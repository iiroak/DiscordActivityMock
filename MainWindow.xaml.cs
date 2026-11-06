using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using Windows.Graphics;

namespace DiscordActivityMockV2
{
    public sealed partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);

        private const int SW_HIDE = 0;
        private const int SW_MINIMIZE = 6;
        private const int MaxCatalogBytes = 2 * 1024 * 1024;
        private const string DefaultCustomFolder = "Accessories";
        private const string DefaultCustomExe = "M1-Win64-Shipping";

        private static readonly string settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DiscordActivityMock", "settings.json");
        private static readonly string tempSessionsPath = Path.Combine(
            Path.GetTempPath(), "DiscordActivityMock", "Sessions");

        private static readonly string githubActivityList = "https://raw.githubusercontent.com/iiroak/DiscordActivityMock/refs/heads/main/resources/ActivityList.json";
        private static readonly HttpClient activityHttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

        // Private WordPad session in %TEMP%\DiscordActivityMock\Sessions\<guid>
        private string? _originalWordPadPath;  // Original WordPad copy inside the session
        private string? _sessionRootPath;
        private FileStream? _sessionLock;
        private string? _workingRootPath;
        private string? _currentFolderPath;    // Current working folder path
        private string? _currentExePath;       // Current executable path
        private string _lastFolderName = "";   // Last folder name used
        private string _lastExeName = "";      // Last exe name used

        // Activity list
        private ActivityItem[]? _downloadedActivities;
        private List<ActivityOption> _activityOptions = new();
        private bool _hasAppliedOption;
        private bool _appliedIsCustom;
        private ActivityItem? _appliedActivity;
        private string _customFolderText = DefaultCustomFolder;
        private string _customExeText = DefaultCustomExe;
        private bool _isCustomActivity;

        // Activity process and state
        private Process? _runningProcess;
        private bool _isActivityRunning;
        private bool _isStarting;
        private bool _isPreparingWordPad;
        private bool _isCatalogLoading;
        private CancellationTokenSource? _hideCancellation;
        private long _activitySessionId;
        private readonly CancellationTokenSource _windowLifetime = new();
        private readonly SemaphoreSlim _dialogSemaphore = new(1, 1);

        // Elapsed time and auto-stop
        private readonly DispatcherTimer _activityTimer;
        private readonly DispatcherTimer _autoStopTimer;
        private readonly Stopwatch _activityStopwatch = new();
        private readonly SolidColorBrush _runningBrush;
        private readonly SolidColorBrush _inactiveBrush;

        // Settings
        private bool _autoStopEnabled = false;
        private int _autoStopMinutes = 30;
        private bool _hideEnabled = true;
        private int _hideDelaySeconds = 15;
        private bool _dontShowStartWarning = false;
        private bool _isDarkTheme = true;

        // Window management
        private readonly AppWindow _appWindow;
        private readonly IntPtr _hwnd;
        private bool _isInitializing = true;
        private bool _isWindowClosed;
        private bool _startupWorkStarted;

        public MainWindow()
        {
            InitializeComponent();
            Localization.DetectLanguage();

            _runningBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 67, 181, 129));
            _inactiveBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 153, 170, 181));
            VersionText.Text = $"v{UpdateChecker.GetCurrentVersion()}";

            LoadSettings();

            _activityTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _activityTimer.Tick += ActivityTimer_Tick;
            _autoStopTimer = new DispatcherTimer();
            _autoStopTimer.Tick += AutoStopTimer_Tick;

            // AppWindow.Resize uses physical pixels; convert the desired 860x400 DIPs at this monitor's DPI.
            _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);
            var workArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary).WorkArea;
            uint dpi = GetDpiForWindow(_hwnd);
            double dpiScale = dpi == 0 ? 1.0 : dpi / 96.0;
            int desiredWidth = (int)Math.Round(860 * dpiScale);
            int desiredHeight = (int)Math.Round(400 * dpiScale);
            int workAreaMargin = (int)Math.Round(32 * dpiScale);
            int initialWidth = Math.Min(desiredWidth, Math.Max(1, workArea.Width - workAreaMargin));
            int initialHeight = Math.Min(desiredHeight, Math.Max(1, workArea.Height - workAreaMargin));
            _appWindow.Resize(new SizeInt32(initialWidth, initialHeight));
            _appWindow.Move(new PointInt32(
                workArea.X + Math.Max(0, (workArea.Width - initialWidth) / 2),
                workArea.Y + Math.Max(0, (workArea.Height - initialHeight) / 2)));
            _appWindow.Changed += AppWindow_Changed;

            try
            {
                string? windowIconPath = ResolveBundledAssetPath(Path.Combine("Assets", "logo.ico"));
                if (windowIconPath != null)
                    _appWindow.SetIcon(windowIconPath);

                string? logoImagePath = ResolveBundledAssetPath(Path.Combine("Assets", "logo.png"));
                if (logoImagePath != null)
                    AppLogoImage.Source = new BitmapImage(new Uri(logoImagePath));
            }
            catch (Exception ex)
            {
                AppLog.Error("Load application logo", ex);
            }

            SetupCustomTitleBar();
            ApplyLocalization();

            // Only the custom activity is listed until a catalog is loaded.
            PopulateActivityList();

            ApplyLoadedSettings();
            UpdateMaximizeButton();
            UpdateControlStates();

            _isInitializing = false;
        }

        private void RootGrid_Loaded(object sender, RoutedEventArgs e)
        {
            if (_startupWorkStarted)
                return;

            _startupWorkStarted = true;
            string currentVersion = AppVersion.Current;
            string? processPath = Environment.ProcessPath;
            _ = Task.Run(() => StartupCleanup.Run(currentVersion, processPath, tempSessionsPath));
            _ = CheckForUpdatesAsync();
        }

        #region Updates

        private async Task CheckForUpdatesAsync(bool showResultWhenCurrent = false)
        {
            try
            {
                UpdateCheckResult check = await UpdateChecker.CheckForUpdateAsync(_windowLifetime.Token);
                if (_isWindowClosed)
                    return;

                if (!check.Succeeded)
                {
                    if (showResultWhenCurrent)
                        await ShowDialogAsync(Localization.Get("Error"), Localization.Get("UpdateCheckFailed"));
                    return;
                }

                if (check.Update == null)
                {
                    if (showResultWhenCurrent)
                        await ShowDialogAsync(Localization.Get("Updates"), Localization.Get("NoUpdatesAvailable"));
                    return;
                }

                // Assets without a SHA-256 digest are not returned, so only "View on GitHub" is offered for them.
                UpdatePackageKind packageKind = UpdateChecker.GetCurrentPackageKind();
                GitHubAsset? asset = UpdateChecker.GetUpdateAsset(check.Update, packageKind);
                var dialog = new ContentDialog
                {
                    Title = Localization.Get("UpdateAvailable"),
                    Content = Localization.Get("UpdateMessage", check.Update.Version),
                    PrimaryButtonText = asset == null ? Localization.Get("ViewOnGitHub") : Localization.Get("UpdateNow"),
                    SecondaryButtonText = asset == null ? "" : Localization.Get("ViewOnGitHub"),
                    CloseButtonText = Localization.Get("Later"),
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.Content.XamlRoot,
                    RequestedTheme = CurrentElementTheme
                };

                ContentDialogResult? result = await ShowContentDialogAsync(dialog);
                if (result == ContentDialogResult.Primary)
                {
                    if (asset == null)
                        UpdateChecker.OpenReleasePage(check.Update.ReleaseUrl);
                    else
                        await DownloadAndApplyUpdateAsync(check.Update, packageKind, asset);
                }
                else if (result == ContentDialogResult.Secondary)
                {
                    UpdateChecker.OpenReleasePage(check.Update.ReleaseUrl);
                }
            }
            catch (Exception ex)
            {
                AppLog.Error("Update check", ex);
                if (showResultWhenCurrent && !_isWindowClosed)
                    await ShowDialogAsync(Localization.Get("Error"), Localization.Get("UpdateCheckFailed"));
            }
        }

        private async Task DownloadAndApplyUpdateAsync(UpdateInfo update, UpdatePackageKind packageKind, GitHubAsset asset)
        {
            if (_isActivityRunning || _isStarting)
            {
                await ShowDialogAsync(Localization.Get("Error"), Localization.Get("StopActivityBeforeUpdate"));
                return;
            }

            string? currentExecutable = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(currentExecutable))
            {
                await ShowDialogAsync(Localization.Get("Error"), Localization.Get("UpdateApplyFailed"));
                return;
            }

            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_windowLifetime.Token);
            cancellation.CancelAfter(TimeSpan.FromMinutes(10));

            var progressText = new TextBlock
            {
                Text = Localization.Get("DownloadingUpdate"),
                TextWrapping = TextWrapping.Wrap
            };
            var progressBar = new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                IsIndeterminate = true,
                Margin = new Thickness(0, 12, 0, 0)
            };
            var progress = new Progress<int>(percent =>
            {
                progressBar.IsIndeterminate = false;
                progressBar.Value = percent;
                progressText.Text = Localization.Get("UpdateDownloadProgress", percent);
            });
            var progressDialog = new ContentDialog
            {
                Title = Localization.Get("DownloadingUpdate"),
                Content = new StackPanel { Spacing = 8, Children = { progressText, progressBar } },
                CloseButtonText = Localization.Get("Cancel"),
                XamlRoot = this.Content.XamlRoot,
                RequestedTheme = CurrentElementTheme
            };

            Task<ContentDialogResult?> dialogTask = ShowContentDialogAsync(progressDialog);
            Task<UpdateDownloadResult> downloadTask = UpdateChecker.DownloadUpdateAsync(asset, progress, cancellation.Token);
            Task firstCompleted = await Task.WhenAny(dialogTask, downloadTask);
            bool cancelledByUser = firstCompleted == dialogTask;
            if (cancelledByUser)
                cancellation.Cancel();
            else
                progressDialog.Hide();

            await dialogTask;
            UpdateDownloadResult download = await downloadTask;
            if (cancelledByUser || _isWindowClosed)
            {
                // The download can finish at the same moment the user cancels.
                if (download.FilePath != null)
                    UpdateChecker.DeleteDownloadedUpdate(download.FilePath);
                return;
            }

            if (download.IntegrityFailed)
            {
                var integrityDialog = new ContentDialog
                {
                    Title = Localization.Get("Error"),
                    Content = Localization.Get("UpdateIntegrityFailed"),
                    PrimaryButtonText = Localization.Get("ViewOnGitHub"),
                    CloseButtonText = Localization.Get("Cancel"),
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.Content.XamlRoot,
                    RequestedTheme = CurrentElementTheme
                };
                if (await ShowContentDialogAsync(integrityDialog) == ContentDialogResult.Primary)
                    UpdateChecker.OpenReleasePage(update.ReleaseUrl);
                return;
            }

            if (download.FilePath == null)
            {
                await ShowDialogAsync(Localization.Get("Error"), Localization.Get("UpdateDownloadFailed"));
                return;
            }

            string downloadedPath = download.FilePath;
            string? updateDirectory = null;
            bool redirectSaved = false;
            // The marker is shared by the original executable and its update copies; restore it if the update fails.
            string? previousRedirect = UpdateChecker.ReadPortableUpdateRedirect(currentExecutable);
            try
            {
                if (packageKind == UpdatePackageKind.Installed)
                {
                    using Process? installer = Process.Start(new ProcessStartInfo
                    {
                        FileName = downloadedPath,
                        Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-",
                        UseShellExecute = true
                    });
                    if (installer == null)
                        throw new InvalidOperationException("Windows did not start the update installer.");

                    // The installer runs from the download folder; startup cleanup removes it later.
                    Application.Current.Exit();
                    return;
                }

                updateDirectory = UpdateChecker.CreatePortableUpdateDirectory(currentExecutable, update.Version);
                string updatedExecutable;
                if (packageKind == UpdatePackageKind.PortableExe)
                {
                    string assetName = Path.GetFileName(downloadedPath);
                    updatedExecutable = Path.Combine(updateDirectory, assetName);
                    File.Copy(downloadedPath, updatedExecutable, overwrite: false);
                }
                else
                {
                    updatedExecutable = UpdateChecker.ExtractPortableZip(
                        downloadedPath,
                        updateDirectory,
                        UpdateChecker.GetPortableExecutableName(update.Version));
                }

                UpdateChecker.SavePortableUpdateRedirect(currentExecutable, updatedExecutable);
                redirectSaved = true;
                using Process? updatedProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = updatedExecutable,
                    WorkingDirectory = Path.GetDirectoryName(updatedExecutable)!,
                    UseShellExecute = true
                });
                if (updatedProcess == null)
                    throw new InvalidOperationException("Windows did not start the updated application.");

                UpdateChecker.DeleteDownloadedUpdate(downloadedPath);
                Application.Current.Exit();
            }
            catch (Exception ex)
            {
                AppLog.Error("Apply update", ex);
                if (redirectSaved)
                    UpdateChecker.RestorePortableUpdateRedirect(currentExecutable, previousRedirect);
                if (updateDirectory != null)
                    UpdateChecker.DeletePortableUpdateDirectory(updateDirectory);
                UpdateChecker.DeleteDownloadedUpdate(downloadedPath);
                await ShowDialogAsync(Localization.Get("Error"), Localization.Get("UpdateApplyFailed"));
            }
        }

        #endregion

        #region Window, theme and localization

        private ElementTheme CurrentElementTheme => _isDarkTheme ? ElementTheme.Dark : ElementTheme.Light;

        private void SetupCustomTitleBar()
        {
            // Extend content into the title bar and hide the default caption buttons.
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(TitleBarBorder);

            if (_appWindow.Presenter is OverlappedPresenter presenter)
                presenter.SetBorderAndTitleBar(true, false);
        }

        private void ApplyLocalization()
        {
            Title = Localization.Get("WindowTitle");
            AppTitleText.Text = Localization.Get("WindowTitle");

            // Step titles
            Step1TitleText.Text = Localization.Get("Step1Title");
            Step2TitleText.Text = Localization.Get("Step2Title");
            Step3TitleText.Text = Localization.Get("Step3Title");

            // Step 1
            CopyFromSystemText.Text = Localization.Get("CopyFromSystem");
            BundledWordPadText.Text = Localization.Get("BundledBackup");
            WordPadStatusBar.Title = Localization.Get("Status");
            WordPadStatusBar.Message = Localization.Get("SelectOptionAbove");

            // Step 2
            ActivitySearchBox.PlaceholderText = Localization.Get("Search");
            SetToolTip(FetchActivitiesBtn, Localization.Get("ReloadActivityList"));
            AutomationProperties.SetName(ActivityListButton, Localization.Get("SelectActivity"));
            FolderNameTxt.Header = Localization.Get("FolderHeader");
            FileExeTxt.Header = Localization.Get("ExeHeader");
            SetToolTip(ClearBtn, Localization.Get("Clear"));

            // Step 3
            StartBtnText.Text = Localization.Get("Start");
            StopBtnText.Text = Localization.Get("Stop");
            SetToolTip(HideSettingsBtn, Localization.Get("HideSettings"));
            HideFlyoutTitle.Text = Localization.Get("HideWordPad");
            HideWordPadCheck.Content = Localization.Get("EnableHide");
            HideDelayLabel.Text = Localization.Get("Delay");
            HideDelayUnit.Text = Localization.Get("SecondsUnit");
            HideDelayHelpText.Text = Localization.Get("HideDelayHelp");
            SetToolTip(TimerSettingsBtn, Localization.Get("TimerSettings"));
            TimerFlyoutTitle.Text = Localization.Get("AutoStop");
            AutoStopCheck.Content = Localization.Get("EnableAutoStop");
            AutoStopLabel.Text = Localization.Get("StopAfter");
            AutoStopUnit.Text = Localization.Get("MinutesUnit");
            AutoStopMaxText.Text = Localization.Get("AutoStopMax");

            // Title bar buttons
            SetToolTip(MinimizeBtn, Localization.Get("Minimize"));
            SetToolTip(CloseBtn, Localization.Get("Close"));
            AutomationProperties.SetName(ThemeToggle, Localization.Get("DarkTheme"));

            // Footer
            GitHubBtnText.Text = Localization.Get("GitHub");
            AboutBtnText.Text = Localization.Get("About");
            MadeWithText.Text = Localization.Get("MadeWith");
            ByText.Text = Localization.Get("By");
            SetToolTip(InfoBtn, Localization.Get("About"));
        }

        /// <summary>Sets the tooltip and the accessible name, so icon-only buttons are announced by screen readers.</summary>
        private static void SetToolTip(FrameworkElement element, string text)
        {
            ToolTipService.SetToolTip(element, text);
            AutomationProperties.SetName(element, text);
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            _isWindowClosed = true;
            _windowLifetime.Cancel();
            _appWindow.Changed -= AppWindow_Changed;
            _activityTimer.Stop();
            _autoStopTimer.Stop();
            StopRunningProcess();
            if (!_isActivityRunning)
                CleanTemporaryFiles();
        }

        private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
        {
            ShowWindow(_hwnd, SW_MINIMIZE);
        }

        private void MaximizeBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_appWindow.Presenter is OverlappedPresenter overlapped)
            {
                if (overlapped.State == OverlappedPresenterState.Maximized)
                    overlapped.Restore();
                else
                    overlapped.Maximize();
            }
            UpdateMaximizeButton();
        }

        /// <summary>Double-clicking the title bar or Win+Up also maximizes; keep the button icon in sync.</summary>
        private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (!_isWindowClosed && (args.DidPresenterChange || args.DidSizeChange))
                UpdateMaximizeButton();
        }

        private void UpdateMaximizeButton()
        {
            bool maximized = _appWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Maximized };
            MaximizeIcon.Glyph = maximized ? "\uE923" : "\uE922"; // Restore or Maximize icon
            SetToolTip(MaximizeBtn, Localization.Get(maximized ? "Restore" : "Maximize"));
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ThemeToggle_Toggled(object sender, RoutedEventArgs e)
        {
            _isDarkTheme = ThemeToggle.IsOn;
            ApplyTheme(_isDarkTheme);
            SaveSettings();
        }

        private void ApplyTheme(bool isDark)
        {
            if (RootGrid != null)
                RootGrid.RequestedTheme = isDark ? ElementTheme.Dark : ElementTheme.Light;

            // The custom title bar uses fixed colors, so update them for each theme.
            Windows.UI.Color primary = isDark ? Windows.UI.Color.FromArgb(255, 255, 255, 255) : Windows.UI.Color.FromArgb(255, 31, 35, 40);
            Windows.UI.Color secondary = isDark ? Windows.UI.Color.FromArgb(255, 153, 170, 181) : Windows.UI.Color.FromArgb(255, 80, 80, 80);
            var secondaryBrush = new SolidColorBrush(secondary);

            if (TitleBarGrid != null)
            {
                TitleBarGrid.Background = new SolidColorBrush(
                    isDark ? Windows.UI.Color.FromArgb(255, 26, 27, 38) : Windows.UI.Color.FromArgb(255, 240, 240, 245));
            }
            if (AppTitleText != null)
                AppTitleText.Foreground = new SolidColorBrush(primary);
            if (VersionText != null)
                VersionText.Foreground = secondaryBrush;
            if (MinimizeIcon != null)
                MinimizeIcon.Foreground = secondaryBrush;
            if (MaximizeIcon != null)
                MaximizeIcon.Foreground = secondaryBrush;
            if (CloseIcon != null)
                CloseIcon.Foreground = secondaryBrush;
            if (ThemeIcon != null)
            {
                ThemeIcon.Glyph = isDark ? "\uE793" : "\uE706"; // Moon or Sun
                ThemeIcon.Foreground = secondaryBrush;
            }
        }

        #endregion

        #region Control state

        /// <summary>The single place that decides which controls are enabled.</summary>
        private void UpdateControlStates()
        {
            if (_isWindowClosed)
                return;

            bool activityBusy = _isActivityRunning || _isStarting;
            bool sourceBusy = activityBusy || _isPreparingWordPad;
            bool wordPadReady = !string.IsNullOrEmpty(_currentExePath);
            bool canConfigure = wordPadReady && !sourceBusy;
            bool canChangeActivity = canConfigure && !_isCatalogLoading;
            bool hasCatalog = _downloadedActivities is { Length: > 0 };
            bool canEditCustom = canConfigure && _isCustomActivity;

            WordPadSystemBtn.IsEnabled = !sourceBusy;
            BundledWordPadBtn.IsEnabled = !sourceBusy;

            ActivityListCombo.IsEnabled = canChangeActivity;
            ActivityListButton.IsEnabled = canChangeActivity;
            FetchActivitiesBtn.IsEnabled = canChangeActivity;
            ActivitySearchBox.IsEnabled = canChangeActivity && hasCatalog;
            FolderNameTxt.IsEnabled = canEditCustom;
            FileExeTxt.IsEnabled = canEditCustom;
            ClearBtn.IsEnabled = canEditCustom;

            HideSettingsBtn.IsEnabled = canConfigure;
            TimerSettingsBtn.IsEnabled = canConfigure;
            // A catalog reload replaces the selected activity, so starting waits until it finishes.
            StartActivityBtn.IsEnabled = canChangeActivity;
            StopActivityBtn.IsEnabled = _isActivityRunning;

            LoadingRing.IsActive = _isPreparingWordPad || _isCatalogLoading;
            StatusIndicator.Fill = _isActivityRunning ? _runningBrush : _inactiveBrush;
            StatusText.Text = Localization.Get(_isActivityRunning ? "Running" : "Inactive");
        }

        private void ActivityTimer_Tick(object? sender, object e)
        {
            TimerText.Text = ActivityRules.FormatElapsed(_activityStopwatch.Elapsed);
        }

        private void ResetActivityTimers()
        {
            _activityTimer.Stop();
            _autoStopTimer.Stop();
            _activityStopwatch.Reset();
            TimerText.Text = ActivityRules.FormatElapsed(TimeSpan.Zero);
        }

        private void AutoStopTimer_Tick(object? sender, object e)
        {
            _autoStopTimer.Stop();
            if (StopRunningProcess())
            {
                ResetActivityTimers();
            }
            else
            {
                WordPadStatusBar.Severity = InfoBarSeverity.Error;
                WordPadStatusBar.Message = Localization.Get("StopProcessFailed");
            }
            UpdateControlStates();
        }

        #endregion

        #region Settings

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(settingsPath))
                {
                    string json = File.ReadAllText(settingsPath);
                    var settings = JsonSerializer.Deserialize(json, AppJsonContext.Default.AppSettings);
                    if (settings != null)
                    {
                        _hideEnabled = settings.HideEnabled;
                        _autoStopEnabled = settings.AutoStopEnabled;
                        _autoStopMinutes = Math.Clamp(settings.AutoStopMinutes, 1, 1440);
                        _dontShowStartWarning = settings.DontShowStartWarning;
                        _isDarkTheme = settings.IsDarkTheme;
                        _hideDelaySeconds = Math.Clamp(settings.HideDelaySeconds, 1, 60);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Error("Load settings", ex);
            }
        }

        private void SaveSettings()
        {
            if (_isInitializing)
                return;

            string? tempPath = null;
            try
            {
                var settings = new AppSettings
                {
                    HideEnabled = _hideEnabled,
                    HideDelaySeconds = _hideDelaySeconds,
                    AutoStopEnabled = _autoStopEnabled,
                    AutoStopMinutes = _autoStopMinutes,
                    DontShowStartWarning = _dontShowStartWarning,
                    IsDarkTheme = _isDarkTheme
                };

                string? dir = Path.GetDirectoryName(settingsPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonSerializer.Serialize(settings, AppJsonContext.Default.AppSettings);
                tempPath = settingsPath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, settingsPath, true);
            }
            catch (Exception ex)
            {
                AppLog.Error("Save settings", ex);
            }
            finally
            {
                if (tempPath != null && File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }
            }
        }

        private void ApplyLoadedSettings()
        {
            ThemeToggle.IsOn = _isDarkTheme;
            ApplyTheme(_isDarkTheme);
            UpdateHideStatusText();
            UpdateTimerStatusText();
        }

        #endregion

        #region Utility Methods

        private bool StopRunningProcess()
        {
            _hideCancellation?.Cancel();
            _hideCancellation?.Dispose();
            _hideCancellation = null;

            var process = _runningProcess;
            if (process == null)
            {
                _activitySessionId++;
                _isActivityRunning = false;
                return true;
            }

            bool hasExited;
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
                hasExited = process.HasExited;
            }
            catch (Exception ex)
            {
                AppLog.Error("Stop activity process", ex);
                try { hasExited = process.HasExited; }
                catch { hasExited = false; }
            }

            if (!hasExited)
            {
                _isActivityRunning = true;
                return false;
            }

            _activitySessionId++;
            _runningProcess = null;
            _isActivityRunning = false;
            process.Dispose();
            return true;
        }

        private void CleanTemporaryFiles()
        {
            try
            {
                _sessionLock?.Dispose();
                _sessionLock = null;
                if (!string.IsNullOrEmpty(_sessionRootPath) && Directory.Exists(_sessionRootPath) && IsOwnedSessionRoot(_sessionRootPath))
                {
                    Directory.Delete(_sessionRootPath, true);
                }
            }
            catch (Exception ex)
            {
                AppLog.Error("Clean temporary session", ex);
            }
            finally
            {
                _originalWordPadPath = null;
                _sessionRootPath = null;
                _workingRootPath = null;
                _currentFolderPath = null;
                _currentExePath = null;
                _lastFolderName = "";
                _lastExeName = "";
            }
        }

        private string CreateSessionRoot()
        {
            Directory.CreateDirectory(tempSessionsPath);
            string sessionRoot = Path.Combine(tempSessionsPath, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(sessionRoot);
            _sessionRootPath = sessionRoot;
            // Startup cleanup in other instances skips sessions whose lock is held.
            _sessionLock = StartupCleanup.AcquireSessionLock(sessionRoot);
            return sessionRoot;
        }

        private static bool IsOwnedSessionRoot(string path) =>
            PathGuard.IsSamePath(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(path)), tempSessionsPath) &&
            Guid.TryParseExact(Path.GetFileName(Path.TrimEndingDirectorySeparator(path)), "N", out _);

        private static string? ResolveBundledAssetPath(string relativePath)
        {
            string appDirectoryPath = Path.Combine(AppContext.BaseDirectory, relativePath);
            if (File.Exists(appDirectoryPath))
                return appDirectoryPath;

            string processName = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "DiscordActivityMockV2");
            string extractionRoot = Path.Combine(Path.GetTempPath(), ".net", processName);
            if (!Directory.Exists(extractionRoot))
                return null;

            string? newestMatch = null;
            DateTime newestTime = DateTime.MinValue;
            foreach (string directory in Directory.GetDirectories(extractionRoot))
            {
                string candidate = Path.Combine(directory, relativePath);
                DateTime writeTime = Directory.GetLastWriteTimeUtc(directory);
                if (File.Exists(candidate) && writeTime >= newestTime)
                {
                    newestMatch = candidate;
                    newestTime = writeTime;
                }
            }
            return newestMatch;
        }

        /// <summary>
        /// Resets the working folder to original WordPad state for re-renaming
        /// </summary>
        private bool ResetToOriginal()
        {
            if (string.IsNullOrEmpty(_originalWordPadPath) || !Directory.Exists(_originalWordPadPath) ||
                string.IsNullOrEmpty(_workingRootPath) || string.IsNullOrEmpty(_sessionRootPath) ||
                !IsOwnedSessionRoot(_sessionRootPath) || !PathGuard.IsWithin(_sessionRootPath, _workingRootPath))
                return false;

            try
            {
                string fullWorkingRoot = Path.GetFullPath(_workingRootPath);
                if (Directory.Exists(fullWorkingRoot))
                    Directory.Delete(fullWorkingRoot, true);

                string workingPath = Path.Combine(fullWorkingRoot, "WordPad");
                Directory.CreateDirectory(workingPath);

                CopyWordPadFiles(_originalWordPadPath, workingPath);

                _currentFolderPath = workingPath;
                _currentExePath = Path.Combine(workingPath, "wordpad.exe");
                _lastFolderName = "";
                _lastExeName = "";

                return true;
            }
            catch (Exception ex)
            {
                AppLog.Error("Restore isolated WordPad copy", ex);
                return false;
            }
        }

        private async Task ShowDialogAsync(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = Localization.Get("OK"),
                XamlRoot = this.Content.XamlRoot,
                RequestedTheme = CurrentElementTheme
            };
            await ShowContentDialogAsync(dialog);
        }

        private async Task<ContentDialogResult?> ShowContentDialogAsync(ContentDialog dialog)
        {
            await _dialogSemaphore.WaitAsync();
            try
            {
                if (_isWindowClosed)
                    return null;
                try
                {
                    return await dialog.ShowAsync();
                }
                catch when (_isWindowClosed)
                {
                    return null;
                }
            }
            finally
            {
                _dialogSemaphore.Release();
            }
        }

        private static void CopyWordPadFiles(string sourceDirectory, string destinationDirectory)
        {
            foreach (string sourceFile in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(sourceDirectory, sourceFile);
                string destinationFile = Path.Combine(destinationDirectory, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
                File.Copy(sourceFile, destinationFile, overwrite: true);
            }
        }

        private static void RenameWordPadMuiFiles(string workingDirectory, string sourceExecutableName, string targetExecutableName)
        {
            string sourceMuiName = Path.GetFileName(sourceExecutableName) + ".mui";
            foreach (string sourceMuiPath in Directory.EnumerateFiles(workingDirectory, sourceMuiName, SearchOption.AllDirectories))
            {
                string targetMuiPath = Path.Combine(
                    Path.GetDirectoryName(sourceMuiPath)!,
                    targetExecutableName + ".exe.mui");
                if (File.Exists(targetMuiPath))
                    throw new IOException(Localization.Get("MuiExists"));
                File.Move(sourceMuiPath, targetMuiPath);
            }
        }

        private async Task<bool> ShowStartWarningDialog()
        {
            var dontShowCheckBox = new CheckBox
            {
                Content = Localization.Get("DontShowAgain")
            };

            var contentPanel = new StackPanel
            {
                Spacing = 12
            };

            contentPanel.Children.Add(new TextBlock
            {
                Text = Localization.Get("WarningMessage1"),
                TextWrapping = TextWrapping.Wrap
            });

            contentPanel.Children.Add(new TextBlock
            {
                Text = Localization.Get("WarningMessage2"),
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 153, 170, 181))
            });

            contentPanel.Children.Add(new TextBlock
            {
                Text = Localization.Get("WarningMessage3"),
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 250, 166, 26))
            });

            contentPanel.Children.Add(dontShowCheckBox);

            var dialog = new ContentDialog
            {
                Title = Localization.Get("BeforeStarting"),
                Content = contentPanel,
                PrimaryButtonText = Localization.Get("Start"),
                CloseButtonText = Localization.Get("Cancel"),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot,
                RequestedTheme = CurrentElementTheme
            };

            var result = await ShowContentDialogAsync(dialog);

            if (dontShowCheckBox.IsChecked == true)
            {
                _dontShowStartWarning = true;
                SaveSettings();
            }

            return result == ContentDialogResult.Primary;
        }

        #endregion

        #region Step 1: Copy or Prepare Bundled WordPad

        private async void WordPadSystemBtn_Click(object sender, RoutedEventArgs e) =>
            await PrepareWordPadAsync(CopySystemWordPad, "Copy system WordPad", "WordPadUnavailable");

        private async void WordPadBundledBtn_Click(object sender, RoutedEventArgs e) =>
            await PrepareWordPadAsync(CopyBundledWordPad, "Prepare bundled x86 WordPad", "BundledWordPadUnavailable");

        /// <summary>Creates a new private session with a WordPad copy, then loads the activity catalog.</summary>
        private async Task PrepareWordPadAsync(Func<string> copyWordPad, string operation, string failureTitleKey)
        {
            if (_isWindowClosed || _isPreparingWordPad)
                return;

            if (_isActivityRunning || _isStarting)
            {
                await ShowDialogAsync(Localization.Get("ActivityRunningTitle"), Localization.Get("StopBeforeChangingSource"));
                return;
            }

            _isPreparingWordPad = true;
            UpdateControlStates();
            bool ready = false;
            try
            {
                CleanTemporaryFiles();
                WordPadStatusBar.Severity = InfoBarSeverity.Informational;
                WordPadStatusBar.Message = Localization.Get("PreparingWordPad");

                SetupWordPadPaths(copyWordPad());
                WordPadStatusBar.Severity = InfoBarSeverity.Success;
                WordPadStatusBar.Message = Localization.Get("WordPadReady");
                ready = true;
            }
            catch (Exception ex)
            {
                AppLog.Error(operation, ex);
                CleanTemporaryFiles();
                WordPadStatusBar.Severity = InfoBarSeverity.Error;
                WordPadStatusBar.Message = Localization.Get("WordPadSetupFailed");
                await ShowDialogAsync(Localization.Get(failureTitleKey), ex.Message);
            }
            finally
            {
                _isPreparingWordPad = false;
                UpdateControlStates();
            }

            if (ready)
                await AutoFetchActivities();
        }

        private string CopySystemWordPad()
        {
            string programFiles = Environment.GetEnvironmentVariable("ProgramW6432") ??
                                  Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pathWordPad = Path.Combine(programFiles, "Windows NT", "Accessories");
            if (!Directory.Exists(pathWordPad) || !File.Exists(Path.Combine(pathWordPad, "wordpad.exe")))
                throw new FileNotFoundException(Localization.Get("WordPadNotFound"));

            string backupPath = Path.Combine(CreateSessionRoot(), "Original");
            Directory.CreateDirectory(backupPath);
            CopyWordPadFiles(pathWordPad, backupPath);
            PeImage.ValidateWordPadExecutable(Path.Combine(backupPath, "wordpad.exe"));
            return backupPath;
        }

        private string CopyBundledWordPad()
        {
            string backupPath = Path.Combine(CreateSessionRoot(), "Original");
            Directory.CreateDirectory(backupPath);
            foreach (string fileName in new[] { "wordpad.exe", "WordpadFilter.dll" })
            {
                string? sourceFile = ResolveBundledAssetPath(Path.Combine("resources", fileName));
                if (sourceFile == null)
                    throw new FileNotFoundException(Localization.Get("BundledFileMissing"), fileName);

                File.Copy(sourceFile, Path.Combine(backupPath, fileName), overwrite: true);
            }

            PeImage.ValidateWordPadExecutable(Path.Combine(backupPath, "wordpad.exe"));
            return backupPath;
        }

        private void SetupWordPadPaths(string backupPath)
        {
            if (string.IsNullOrEmpty(_sessionRootPath) || !IsOwnedSessionRoot(_sessionRootPath) ||
                !PathGuard.IsWithin(_sessionRootPath, backupPath))
                throw new InvalidOperationException(Localization.Get("WordPadOutsideSession"));

            _originalWordPadPath = backupPath;
            _workingRootPath = Path.Combine(_sessionRootPath, "Working");
            if (!ResetToOriginal())
                throw new IOException(Localization.Get("WorkingCopyFailed"));
        }

        #endregion

        #region Step 2: Select Activity

        private ActivityOption? GetSelectedOption()
        {
            int index = ActivityListCombo.SelectedIndex;
            return index >= 0 && index < _activityOptions.Count ? _activityOptions[index] : null;
        }

        private ActivityItem? GetSelectedActivityItem() => GetSelectedOption()?.Activity;

        private void ActivityListCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isStarting || _isActivityRunning)
                return;

            ActivityOption? option = GetSelectedOption();
            if (option != null)
                ApplySelectedOption(option);
        }

        /// <summary>
        /// Fills the folder and exe fields only when the selected activity actually changes, so rebuilding the
        /// list (search, catalog reload) keeps what the user typed for the custom activity.
        /// </summary>
        private void ApplySelectedOption(ActivityOption option)
        {
            bool changed = !_hasAppliedOption ||
                           option.IsCustom != _appliedIsCustom ||
                           !ReferenceEquals(option.Activity, _appliedActivity);
            if (changed)
            {
                if (_hasAppliedOption && _appliedIsCustom)
                {
                    _customFolderText = FolderNameTxt.Text;
                    _customExeText = FileExeTxt.Text;
                }

                if (option.Activity == null)
                {
                    FolderNameTxt.Text = _customFolderText;
                    FileExeTxt.Text = _customExeText;
                }
                else
                {
                    FolderNameTxt.Text = option.Activity.folder;
                    FileExeTxt.Text = ActivityRules.StripExeExtension(option.Activity.exe);
                }

                _hasAppliedOption = true;
                _appliedIsCustom = option.IsCustom;
                _appliedActivity = option.Activity;
            }

            _isCustomActivity = option.IsCustom;
            UpdateControlStates();
        }

        private void ActivityListButton_Click(object sender, RoutedEventArgs e)
        {
            if (ActivityListPopup.IsOpen)
            {
                ActivityListPopup.IsOpen = false;
                return;
            }

            RefreshActivityPopupItems();
            Point comboPosition = ActivityListCombo.TransformToVisual(RootGrid).TransformPoint(new Point(0, 0));
            ActivityPopupList.Width = ActivityListCombo.ActualWidth;
            double availableBelow = RootGrid.ActualHeight - comboPosition.Y - ActivityListCombo.ActualHeight - 4;
            ActivityPopupList.Height = Math.Max(80, Math.Min(300, availableBelow - 6));
            ActivityListPopup.IsOpen = true;
            ActivityPopupList.Focus(FocusState.Programmatic);
        }

        private void RefreshActivityPopupItems()
        {
            ActivityPopupList.Items.Clear();
            foreach (ActivityOption option in _activityOptions)
                ActivityPopupList.Items.Add(option.DisplayName);

            int selectedIndex = ActivityListCombo.SelectedIndex;
            ActivityPopupList.SelectedIndex = selectedIndex;
            if (ActivityListPopup.IsOpen && selectedIndex >= 0 && selectedIndex < ActivityPopupList.Items.Count)
                ActivityPopupList.ScrollIntoView(ActivityPopupList.Items[selectedIndex]);
        }

        private void ActivityPopupList_ItemClick(object sender, ItemClickEventArgs e)
        {
            string? clicked = e.ClickedItem?.ToString();
            CommitActivityPopupSelection(_activityOptions.FindIndex(option => option.DisplayName == clicked));
        }

        private void ActivityPopupList_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                CommitActivityPopupSelection(ActivityPopupList.SelectedIndex);
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Escape)
            {
                ActivityListPopup.IsOpen = false;
                ActivityListButton.Focus(FocusState.Programmatic);
                e.Handled = true;
            }
        }

        private void CommitActivityPopupSelection(int index)
        {
            if (index < 0 || index >= _activityOptions.Count)
                return;

            ActivityListCombo.SelectedIndex = index;
            ActivityListPopup.IsOpen = false;
            ActivityListButton.Focus(FocusState.Programmatic);
        }

        private async Task<bool> ConfirmSteamManifestSetupAsync(SteamManifestItem manifest)
        {
            var dialog = new ContentDialog
            {
                Title = Localization.Get("SteamManifestRequiredTitle"),
                Content = new TextBlock
                {
                    Text = Localization.Get("SteamManifestRequiredMessage", manifest.appId),
                    TextWrapping = TextWrapping.Wrap
                },
                PrimaryButtonText = Localization.Get("CreateAndStart"),
                CloseButtonText = Localization.Get("Cancel"),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot,
                RequestedTheme = CurrentElementTheme
            };

            return await ShowContentDialogAsync(dialog) == ContentDialogResult.Primary;
        }

        private async Task AutoFetchActivities()
        {
            if (_downloadedActivities is { Length: > 0 } || _isCatalogLoading || _isWindowClosed)
                return;

            await LoadCatalogAsync(userRequested: false);
        }

        private async void FetchActivitiesBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isActivityRunning || _isStarting || _isCatalogLoading || _isWindowClosed)
                return;

            await LoadCatalogAsync(userRequested: true);
        }

        /// <summary>
        /// Loads the online catalog, or the bundled one when offline. Automatic loads report in the status bar;
        /// user-requested reloads show a dialog and clear the search.
        /// </summary>
        private async Task LoadCatalogAsync(bool userRequested)
        {
            _isCatalogLoading = true;
            UpdateControlStates();
            try
            {
                var (activities, usedFallback) = await LoadActivitiesAsync();
                if (_isWindowClosed)
                    return;

                ApplyActivities(activities);
                if (userRequested)
                {
                    ActivitySearchBox.Text = "";
                    if (usedFallback)
                        await ShowDialogAsync(Localization.Get("OfflineModeTitle"), Localization.Get("OfflineModeMessage"));
                }
                else if (usedFallback)
                {
                    WordPadStatusBar.Message = Localization.Get("OfflineCatalogLoaded");
                }
            }
            catch (OperationCanceledException) when (_windowLifetime.IsCancellationRequested)
            {
                // The window is closing.
            }
            catch (Exception ex)
            {
                AppLog.Error("Load activity catalog", ex);
                if (_isWindowClosed)
                    return;

                if (userRequested)
                {
                    await ShowDialogAsync(Localization.Get("CatalogErrorTitle"), ex.Message);
                }
                else
                {
                    WordPadStatusBar.Severity = InfoBarSeverity.Warning;
                    WordPadStatusBar.Message = Localization.Get("CatalogUnavailable", ex.Message);
                }
            }
            finally
            {
                _isCatalogLoading = false;
                UpdateControlStates();
            }
        }

        private async Task<(ActivityItem[] activities, bool usedFallback)> LoadActivitiesAsync()
        {
            CancellationToken cancellationToken = _windowLifetime.Token;
            Exception? onlineError = null;
            try
            {
                using var response = await activityHttpClient.GetAsync(githubActivityList, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength > MaxCatalogBytes)
                    throw new InvalidDataException(Localization.Get("CatalogTooLarge"));

                await using Stream content = await response.Content.ReadAsStreamAsync(cancellationToken);
                string json = await BoundedRead.ReadUtf8Async(content, MaxCatalogBytes, cancellationToken);
                return (ActivityRules.ParseActivities(json), false);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested &&
                                       ex is HttpRequestException or OperationCanceledException or JsonException or InvalidDataException or IOException)
            {
                // HttpClient timeouts surface as OperationCanceledException; use the bundled list instead.
                AppLog.Warning("Download activity catalog", $"{ex.GetType().Name}: {ex.Message}");
                onlineError = ex;
            }

            using Stream? bundledStream = typeof(MainWindow).Assembly.GetManifestResourceStream("DiscordActivityMockV2.ActivityList.json");
            if (bundledStream == null)
                throw new IOException(Localization.Get("CatalogMissing"), onlineError);

            using var reader = new StreamReader(bundledStream);
            return (ActivityRules.ParseActivities(await reader.ReadToEndAsync(cancellationToken)), true);
        }

        private void ApplyActivities(ActivityItem[] activities)
        {
            _downloadedActivities = activities;
            PopulateActivityList(ActivitySearchBox.Text);
        }

        private void PopulateActivityList(string? searchFilter = null, bool resetSelectionWhenSearchCleared = false)
        {
            ActivityOption? previous = GetSelectedOption();
            List<ActivityOption> options = ActivityRules.BuildOptions(
                _downloadedActivities, searchFilter, Localization.Get("CustomActivity"));
            int selectedIndex = ActivityRules.ChooseSelectionIndex(options, searchFilter, previous, resetSelectionWhenSearchCleared);

            _activityOptions = options;
            ActivityListCombo.Items.Clear();
            foreach (ActivityOption option in options)
                ActivityListCombo.Items.Add(option.DisplayName);

            ActivityListCombo.SelectedIndex = selectedIndex;
            RefreshActivityPopupItems();
        }

        private void ActivitySearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isActivityRunning || _isStarting)
                return;
            PopulateActivityList(ActivitySearchBox.Text, resetSelectionWhenSearchCleared: true);
        }

        #endregion

        #region Step 3: Run Activity

        private async void StartActivityBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isStarting || _isActivityRunning || _isCatalogLoading)
                return;

            _isStarting = true;
            UpdateControlStates();
            try
            {
                if (!_dontShowStartWarning && !await ShowStartWarningDialog())
                    return;

                ActivityItem? selectedActivity = GetSelectedActivityItem();
                SteamManifestItem? steamManifest = selectedActivity?.steamManifest;
                if (steamManifest != null && !await ConfirmSteamManifestSetupAsync(steamManifest))
                    return;

                if (string.IsNullOrEmpty(_workingRootPath) || string.IsNullOrEmpty(_currentExePath) ||
                    string.IsNullOrEmpty(_currentFolderPath) || !File.Exists(_currentExePath) || !Directory.Exists(_currentFolderPath))
                {
                    await ShowDialogAsync(Localization.Get("SelectWordPadFirst"), Localization.Get("WordPadRequiredMessage"));
                    return;
                }

                string targetFolderName = FolderNameTxt.Text.Trim();
                string targetExeName;
                string newFolderPath;
                try
                {
                    targetExeName = ActivityRules.NormalizeExecutableName(FileExeTxt.Text);
                    newFolderPath = ActivityRules.ResolveChildPath(_workingRootPath, targetFolderName);
                    if (steamManifest != null)
                    {
                        SteamManifestSupport.Validate(steamManifest, targetFolderName);
                        string catalogExeName = ActivityRules.NormalizeExecutableName(selectedActivity!.exe);
                        if (!string.Equals(targetExeName, catalogExeName, StringComparison.OrdinalIgnoreCase))
                            throw new ArgumentException(Localization.Get("SteamExeMismatch"));
                    }
                }
                catch (ArgumentException ex)
                {
                    await ShowDialogAsync(Localization.Get("InvalidActivityTitle"), ex.Message);
                    return;
                }

                string normalizedFolderName = Path.GetRelativePath(_workingRootPath, newFolderPath);
                bool needsReset = !string.IsNullOrEmpty(_lastFolderName) &&
                                  (!string.Equals(_lastFolderName, normalizedFolderName, StringComparison.OrdinalIgnoreCase) ||
                                   !string.Equals(_lastExeName, targetExeName, StringComparison.OrdinalIgnoreCase));
                if (needsReset && !ResetToOriginal())
                {
                    await ShowDialogAsync(Localization.Get("SetupErrorTitle"), Localization.Get("RestoreWorkingCopyFailed"));
                    return;
                }

                if (!PathGuard.IsWithin(_workingRootPath, _currentFolderPath))
                    throw new InvalidOperationException(Localization.Get("WorkingFolderOutsideSession"));

                string activityExePath;
                string activityWorkingDirectory;
                if (steamManifest != null)
                {
                    SteamManifestLaunch steamLaunch = SteamManifestSupport.Prepare(
                        steamManifest,
                        _currentFolderPath,
                        _currentExePath,
                        targetExeName);
                    activityExePath = steamLaunch.ExecutablePath;
                    activityWorkingDirectory = steamLaunch.WorkingDirectory;
                    WordPadStatusBar.Severity = InfoBarSeverity.Success;
                    WordPadStatusBar.Message = Localization.Get("SteamManifestReady");
                }
                else
                {
                    string currentExeName = Path.GetFileNameWithoutExtension(_currentExePath);
                    string currentExeDirectory = Path.GetDirectoryName(_currentExePath)!;
                    if (!currentExeName.Equals(targetExeName, StringComparison.OrdinalIgnoreCase))
                    {
                        string newExePath = Path.Combine(currentExeDirectory, targetExeName + ".exe");
                        if (File.Exists(newExePath))
                            throw new IOException(Localization.Get("TargetExeExists"));
                        RenameWordPadMuiFiles(_currentFolderPath, Path.GetFileName(_currentExePath), targetExeName);
                        File.Move(_currentExePath, newExePath);
                        _currentExePath = newExePath;
                    }

                    string currentFolderPath = Path.GetFullPath(_currentFolderPath);
                    if (!string.Equals(currentFolderPath, newFolderPath, StringComparison.OrdinalIgnoreCase))
                    {
                        if (Directory.Exists(newFolderPath) || File.Exists(newFolderPath))
                            throw new IOException(Localization.Get("TargetFolderExists"));

                        if (PathGuard.IsWithin(currentFolderPath, newFolderPath))
                            throw new ArgumentException(Localization.Get("FolderInsideWorkingFolder"));

                        Directory.CreateDirectory(Path.GetDirectoryName(newFolderPath)!);
                        Directory.Move(_currentFolderPath, newFolderPath);
                        _currentFolderPath = newFolderPath;
                        _currentExePath = Path.Combine(newFolderPath, Path.GetFileName(_currentExePath));
                    }

                    activityExePath = _currentExePath;
                    activityWorkingDirectory = _currentFolderPath;
                }

                _lastFolderName = normalizedFolderName;
                _lastExeName = targetExeName;

                var processInfo = new ProcessStartInfo
                {
                    FileName = activityExePath,
                    WorkingDirectory = activityWorkingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = false,
                    WindowStyle = ProcessWindowStyle.Normal
                };

                var process = new Process { StartInfo = processInfo };
                long sessionId = ++_activitySessionId;
                process.Exited += (_, _) => DispatcherQueue.TryEnqueue(() => HandleProcessExited(process, sessionId));
                if (!process.Start())
                {
                    process.Dispose();
                    throw new InvalidOperationException(Localization.Get("ProcessStartFailed"));
                }

                _runningProcess = process;
                _isActivityRunning = true;
                process.EnableRaisingEvents = true;

                _hideCancellation?.Cancel();
                _hideCancellation?.Dispose();
                _hideCancellation = new CancellationTokenSource();
                if (_hideEnabled)
                    _ = HideProcessAfterDelayAsync(process, sessionId, _hideDelaySeconds, _hideCancellation.Token);

                _activityStopwatch.Restart();
                TimerText.Text = ActivityRules.FormatElapsed(TimeSpan.Zero);
                _activityTimer.Start();
                if (_autoStopEnabled)
                {
                    _autoStopTimer.Interval = TimeSpan.FromMinutes(_autoStopMinutes);
                    _autoStopTimer.Start();
                }

                UpdateControlStates();
                if (process.HasExited)
                    DispatcherQueue.TryEnqueue(() => HandleProcessExited(process, sessionId));
            }
            catch (Exception ex)
            {
                AppLog.Error("Start activity", ex);
                bool stopped = !_isActivityRunning || StopRunningProcess();
                if (stopped)
                {
                    ResetActivityTimers();
                    ResetToOriginal();
                }
                else
                {
                    WordPadStatusBar.Severity = InfoBarSeverity.Error;
                    WordPadStatusBar.Message = Localization.Get("PartialStopFailed");
                }
                await ShowDialogAsync(Localization.Get("StartFailedTitle"), ex.Message);
            }
            finally
            {
                _isStarting = false;
                UpdateControlStates();
            }
        }

        private void HandleProcessExited(Process process, long sessionId)
        {
            if (sessionId != _activitySessionId || !ReferenceEquals(_runningProcess, process))
                return;

            _runningProcess = null;
            _isActivityRunning = false;
            _hideCancellation?.Cancel();
            _hideCancellation?.Dispose();
            _hideCancellation = null;
            // Keep the final elapsed time visible until the next start.
            _activityTimer.Stop();
            _autoStopTimer.Stop();
            _activityStopwatch.Stop();
            process.Dispose();
            UpdateControlStates();
        }

        private async Task HideProcessAfterDelayAsync(Process process, long sessionId, int delaySeconds, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                DateTime retryUntil = DateTime.UtcNow.AddSeconds(5);
                while (DateTime.UtcNow <= retryUntil && !cancellationToken.IsCancellationRequested)
                {
                    if (process.HasExited)
                        return;

                    process.Refresh();
                    IntPtr processWindow = process.MainWindowHandle;
                    if (processWindow != IntPtr.Zero)
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            if (sessionId == _activitySessionId && ReferenceEquals(_runningProcess, process) &&
                                !cancellationToken.IsCancellationRequested && !process.HasExited)
                                ShowWindow(processWindow, SW_HIDE);
                        });
                        return;
                    }

                    await Task.Delay(250, cancellationToken);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                AppLog.Error("Hide WordPad window", ex);
            }
        }

        private void StopActivityBtn_Click(object sender, RoutedEventArgs e)
        {
            if (StopRunningProcess())
            {
                ResetActivityTimers();
            }
            else
            {
                WordPadStatusBar.Severity = InfoBarSeverity.Error;
                WordPadStatusBar.Message = Localization.Get("StopProcessFailed");
            }
            UpdateControlStates();
        }

        private void HideFlyout_Opening(object sender, object e)
        {
            // Sync UI controls with saved values when flyout opens
            if (HideWordPadCheck != null)
                HideWordPadCheck.IsChecked = _hideEnabled;
            if (HideDelaySeconds != null)
                HideDelaySeconds.Value = _hideDelaySeconds;
        }

        private void TimerFlyout_Opening(object sender, object e)
        {
            // Sync UI controls with saved values when flyout opens
            if (AutoStopCheck != null)
                AutoStopCheck.IsChecked = _autoStopEnabled;
            if (AutoStopMinutes != null)
            {
                AutoStopMinutes.Value = _autoStopMinutes;
                AutoStopMinutes.IsEnabled = _autoStopEnabled;
            }
        }

        private void AutoStopCheck_Changed(object sender, RoutedEventArgs e)
        {
            _autoStopEnabled = AutoStopCheck?.IsChecked == true;
            if (AutoStopMinutes != null)
            {
                AutoStopMinutes.IsEnabled = _autoStopEnabled;
            }
            UpdateTimerStatusText();
            SaveSettings();
        }

        private void AutoStopMinutes_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (double.IsFinite(args.NewValue))
            {
                _autoStopMinutes = (int)Math.Clamp(args.NewValue, 1, 1440);
            }
            UpdateTimerStatusText();
            SaveSettings();
        }

        private void UpdateTimerStatusText()
        {
            if (TimerStatusText == null) return;

            TimerStatusText.Text = _autoStopEnabled
                ? Localization.Get("TimerStatusOn", _autoStopMinutes)
                : Localization.Get("TimerStatusOff");
        }

        private void HideWordPadCheck_Changed(object sender, RoutedEventArgs e)
        {
            _hideEnabled = HideWordPadCheck?.IsChecked == true;
            UpdateHideStatusText();
            SaveSettings();
        }

        private void HideDelaySeconds_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (double.IsFinite(args.NewValue))
            {
                _hideDelaySeconds = (int)Math.Clamp(args.NewValue, 1, 60);
            }
            UpdateHideStatusText();
            SaveSettings();
        }

        private void UpdateHideStatusText()
        {
            if (HideStatusText == null) return;

            HideStatusText.Text = _hideEnabled
                ? Localization.Get("HideStatusOn", _hideDelaySeconds)
                : Localization.Get("HideStatusOff");
        }

        private void ClearBtn_Click(object sender, RoutedEventArgs e)
        {
            FolderNameTxt.Text = "";
            FileExeTxt.Text = "";
        }

        #endregion

        #region Social Links

        private void GitHubBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://github.com/iiroak");
        }

        private async void InfoBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = Localization.Get("AboutTitle"),
                Content = $"{Localization.Get("AboutContent")}\n\n{Localization.Get("VersionLabel", UpdateChecker.GetCurrentVersion())}",
                PrimaryButtonText = Localization.Get("CheckForUpdates"),
                CloseButtonText = Localization.Get("OK"),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot,
                RequestedTheme = CurrentElementTheme
            };
            if (await ShowContentDialogAsync(dialog) == ContentDialogResult.Primary)
                await CheckForUpdatesAsync(showResultWhenCurrent: true);
        }

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                })?.Dispose();
            }
            catch (Exception ex)
            {
                AppLog.Error("Open external URL", ex);
            }
        }

        #endregion
    }
}
