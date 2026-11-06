using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using Windows.Graphics;

namespace DiscordActivityMockV2
{
    public sealed partial class MainWindow : Window
    {
        // Win32 API for hiding windows
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);
        
        private const int SW_HIDE = 0;
        
        private static readonly string settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DiscordActivityMock", "settings.json");
        private static readonly string tempSessionsPath = Path.Combine(
            Path.GetTempPath(), "DiscordActivityMock", "Sessions");

        private static readonly string githubActivityList = "https://raw.githubusercontent.com/iiroak/DiscordActivityMock/refs/heads/main/resources/ActivityList.json";
        private static readonly HttpClient activityHttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
        
        private string? _originalWordPadPath;  // Path to original WordPad backup
        private string? _sessionRootPath;
        private FileStream? _sessionLock;
        private string? _workingRootPath;
        private string? _currentFolderPath;    // Current working folder path
        private string? _currentExePath;       // Current executable path
        private string _lastFolderName = "";   // Last folder name used
        private string _lastExeName = "";      // Last exe name used
        private ActivityItem[]? _downloadedActivities;
        private Process? _runningProcess;
        private bool _isActivityRunning = false;
        private bool _isStarting = false;
        private bool _isCustomActivity = false;
        
        // Timer for tracking activity duration
        private DispatcherTimer? _activityTimer;
        private DateTime _activityStartTime;
        
        // Auto-stop timer
        private DispatcherTimer? _autoStopTimer;
        private bool _autoStopEnabled = false;
        private int _autoStopMinutes = 30;
        private CancellationTokenSource? _hideCancellation;
        private long _activitySessionId;
        private readonly CancellationTokenSource _windowLifetime = new();
        private readonly SemaphoreSlim _dialogSemaphore = new(1, 1);
        
        // Hide settings
        private bool _hideEnabled = true;
        private int _hideDelaySeconds = 15;
        
        // Settings
        private bool _dontShowStartWarning = false;
        
        // Window management
        private AppWindow? _appWindow;
        private IntPtr _hwnd;
        private bool _isDarkTheme = true;
        private bool _isInitializing = true;
        private bool _isWindowClosed;
        private bool _startupUpdateCheckStarted;

        public MainWindow()
        {
            InitializeComponent();
            VersionText.Text = $"v{UpdateChecker.GetCurrentVersion()}";
            
            // Detect system language
            Localization.DetectLanguage();
            
            // Load saved settings
            LoadSettings();
            
            // Initialize activity timer
            _activityTimer = new DispatcherTimer();
            _activityTimer.Interval = TimeSpan.FromSeconds(1);
            _activityTimer.Tick += ActivityTimer_Tick;
            
            // Initialize auto-stop timer
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
            
            // Setup custom titlebar (fully custom, hide system buttons)
            SetupCustomTitleBar();
            
            // Initialize UI
            ActivityListCombo.Items.Add("Custom Activity");
            ActivityListCombo.SelectedIndex = 0;
            RefreshActivityPopupItems();
            
            // Apply loaded settings to UI
            ApplyLoadedSettings();
            
            // Apply localization to UI
            ApplyLocalization();

            _isInitializing = false;
            
        }

        private void RootGrid_Loaded(object sender, RoutedEventArgs e)
        {
            if (_startupUpdateCheckStarted)
                return;

            _startupUpdateCheckStarted = true;
            string currentVersion = AppVersion.Current;
            string? processPath = Environment.ProcessPath;
            _ = Task.Run(() => StartupCleanup.Run(currentVersion, processPath, tempSessionsPath));
            _ = CheckForUpdatesAsync();
        }

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
                    RequestedTheme = _isDarkTheme ? ElementTheme.Dark : ElementTheme.Light
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
                RequestedTheme = _isDarkTheme ? ElementTheme.Dark : ElementTheme.Light
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
                    RequestedTheme = _isDarkTheme ? ElementTheme.Dark : ElementTheme.Light
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
                    UpdateChecker.RemovePortableUpdateRedirect(currentExecutable);
                if (updateDirectory != null)
                    UpdateChecker.DeletePortableUpdateDirectory(updateDirectory);
                UpdateChecker.DeleteDownloadedUpdate(downloadedPath);
                await ShowDialogAsync(Localization.Get("Error"), Localization.Get("UpdateApplyFailed"));
            }
        }

        private void SetupCustomTitleBar()
        {
            // Extend content into titlebar and hide the default caption buttons
            ExtendsContentIntoTitleBar = true;
            
            // Set the draggable area (the TitleBarBorder element)
            SetTitleBar(TitleBarBorder);
            
            // Get the presenter and configure it
            if (_appWindow != null)
            {
                var presenter = _appWindow.Presenter as OverlappedPresenter;
                if (presenter != null)
                {
                    // This removes the default caption buttons entirely
                    presenter.SetBorderAndTitleBar(true, false);
                }
            }
        }

        private void ApplyLocalization()
        {
            // Step titles
            Step1TitleText.Text = Localization.Get("Step1Title");
            Step2TitleText.Text = Localization.Get("Step2Title");
            Step3TitleText.Text = Localization.Get("Step3Title");
            
            // Buttons
            CopyFromSystemText.Text = Localization.Get("CopyFromSystem");
            BundledWordPadText.Text = Localization.Get("BundledBackup");
            StartBtnText.Text = Localization.Get("Start");
            StopBtnText.Text = Localization.Get("Stop");
            
            // Search placeholder
            ActivitySearchBox.PlaceholderText = Localization.Get("Search");
            
            // Status bar
            WordPadStatusBar.Title = Localization.Get("Status");
            WordPadStatusBar.Message = Localization.Get("SelectOptionAbove");
            
            // Footer
            GitHubBtnText.Text = Localization.Get("GitHub");
            AboutBtnText.Text = Localization.Get("About");
            MadeWithText.Text = Localization.Get("MadeWith");
            ByText.Text = Localization.Get("By");
            
            // Tooltips
            InfoBtn.SetValue(ToolTipService.ToolTipProperty, Localization.Get("About"));
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            _isWindowClosed = true;
            _windowLifetime.Cancel();
            StopRunningProcess();
            if (!_isActivityRunning)
                CleanTemporaryFiles();
        }
        
        #region Window Controls
        
        private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_appWindow != null)
            {
                // Use Win32 to minimize
                ShowWindow(_hwnd, 6); // SW_MINIMIZE = 6
            }
        }
        
        private void MaximizeBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_appWindow != null)
            {
                if (_appWindow.Presenter.Kind == AppWindowPresenterKind.Overlapped)
                {
                    var overlapped = _appWindow.Presenter as OverlappedPresenter;
                    if (overlapped != null)
                    {
                        if (overlapped.State == OverlappedPresenterState.Maximized)
                        {
                            overlapped.Restore();
                            MaximizeIcon.Glyph = "\uE922"; // Maximize icon
                        }
                        else
                        {
                            overlapped.Maximize();
                            MaximizeIcon.Glyph = "\uE923"; // Restore icon
                        }
                    }
                }
            }
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
            {
                RootGrid.RequestedTheme = isDark ? ElementTheme.Dark : ElementTheme.Light;
            }
            
            // Update titlebar colors
            if (TitleBarGrid != null)
            {
                TitleBarGrid.Background = new SolidColorBrush(
                    isDark ? Windows.UI.Color.FromArgb(255, 26, 27, 38) : Windows.UI.Color.FromArgb(255, 240, 240, 245));
            }
            
            // Update theme icon
            if (ThemeIcon != null)
            {
                ThemeIcon.Glyph = isDark ? "\uE793" : "\uE706"; // Moon or Sun
                ThemeIcon.Foreground = new SolidColorBrush(
                    isDark ? Windows.UI.Color.FromArgb(255, 153, 170, 181) : Windows.UI.Color.FromArgb(255, 80, 80, 80));
            }
        }
        
        #endregion

        #region UI Toggle Helpers

        private void ToggleStep2(bool enabled)
        {
            ActivityListCombo.IsEnabled = enabled;
            ActivityListButton.IsEnabled = enabled;
            FetchActivitiesBtn.IsEnabled = enabled;
            ActivitySearchBox.IsEnabled = enabled && _downloadedActivities != null && _downloadedActivities.Length > 0;
            
            // Enable controls when WordPad is ready
            bool wordPadReady = !string.IsNullOrEmpty(_currentExePath);
            HideSettingsBtn.IsEnabled = wordPadReady;
            TimerSettingsBtn.IsEnabled = wordPadReady;
            
            // Enable Start button when WordPad is ready and activity not running
            StartActivityBtn.IsEnabled = enabled && wordPadReady && !_isActivityRunning;
        }
        private void ToggleStep21(bool enabled, bool isCustomActivity = false)
        {
            _isCustomActivity = isCustomActivity;
            bool wordPadReady = !string.IsNullOrEmpty(_currentExePath);
            FolderNameTxt.IsEnabled = enabled && isCustomActivity && !_isActivityRunning;
            FileExeTxt.IsEnabled = enabled && isCustomActivity && !_isActivityRunning;
            ClearBtn.IsEnabled = enabled && isCustomActivity && !_isActivityRunning;
            StartActivityBtn.IsEnabled = enabled && !_isActivityRunning && wordPadReady;
            HideSettingsBtn.IsEnabled = wordPadReady;
        }

        private void SetLoading(bool isLoading)
        {
            LoadingRing.IsActive = isLoading;
            WordPadSystemBtn.IsEnabled = !isLoading;
            BundledWordPadBtn.IsEnabled = !isLoading;
        }

        private void UpdateActivityControls()
        {
            if (_isWindowClosed)
                return;

            bool wordPadReady = !string.IsNullOrEmpty(_currentExePath);
            StartActivityBtn.IsEnabled = !_isActivityRunning && !_isStarting && wordPadReady;
            StopActivityBtn.IsEnabled = _isActivityRunning;
            FolderNameTxt.IsEnabled = !_isActivityRunning && !_isStarting && _isCustomActivity;
            FileExeTxt.IsEnabled = !_isActivityRunning && !_isStarting && _isCustomActivity;
            ClearBtn.IsEnabled = !_isActivityRunning && !_isStarting && _isCustomActivity;
            ActivityListCombo.IsEnabled = !_isActivityRunning && !_isStarting;
            ActivityListButton.IsEnabled = !_isActivityRunning && !_isStarting;
            ActivitySearchBox.IsEnabled = !_isActivityRunning && !_isStarting && wordPadReady;
            FetchActivitiesBtn.IsEnabled = !_isActivityRunning && !_isStarting && wordPadReady;
            WordPadSystemBtn.IsEnabled = !_isActivityRunning && !_isStarting;
            BundledWordPadBtn.IsEnabled = !_isActivityRunning && !_isStarting;
            HideSettingsBtn.IsEnabled = wordPadReady && !_isActivityRunning && !_isStarting;
            TimerSettingsBtn.IsEnabled = wordPadReady && !_isActivityRunning && !_isStarting;
            
            // Update status indicator
            if (_isActivityRunning)
            {
                StatusIndicator.Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 67, 181, 129)); // Green
                StatusText.Text = "Running";
            }
            else
            {
                StatusIndicator.Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 153, 170, 181)); // Gray
                StatusText.Text = "Inactive";
            }
        }
        
        private void ActivityTimer_Tick(object? sender, object e)
        {
            var elapsed = DateTime.Now - _activityStartTime;
            TimerText.Text = elapsed.ToString(@"hh\:mm\:ss");
        }
        
        private void AutoStopTimer_Tick(object? sender, object e)
        {
            _autoStopTimer?.Stop();
            if (StopRunningProcess())
            {
                _activityTimer?.Stop();
                TimerText.Text = "00:00:00";
            }
            else
            {
                WordPadStatusBar.Severity = InfoBarSeverity.Error;
                WordPadStatusBar.Message = "Windows could not stop the activity process.";
            }
            UpdateActivityControls();
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
            // Apply theme
            ThemeToggle.IsOn = _isDarkTheme;
            ApplyTheme(_isDarkTheme);
            
            // Update status texts
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

            return Directory.GetDirectories(extractionRoot)
                .OrderByDescending(Directory.GetLastWriteTimeUtc)
                .Select(directory => Path.Combine(directory, relativePath))
                .FirstOrDefault(File.Exists);
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
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot,
                RequestedTheme = _isDarkTheme ? ElementTheme.Dark : ElementTheme.Light
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
                    throw new IOException("The target executable's localized resource file already exists.");
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
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
            });
            
            contentPanel.Children.Add(new TextBlock
            {
                Text = Localization.Get("WarningMessage2"),
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 153, 170, 181))
            });
            
            contentPanel.Children.Add(new TextBlock
            {
                Text = Localization.Get("WarningMessage3"),
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
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
                RequestedTheme = _isDarkTheme ? ElementTheme.Dark : ElementTheme.Light
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

        private async Task<string> CopyWordPadFolder()
        {
            string programFiles = Environment.GetEnvironmentVariable("ProgramW6432") ??
                                  Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pathWordPad = Path.Combine(programFiles, "Windows NT", "Accessories");
            
            try
            {
                CleanTemporaryFiles();
                if (!Directory.Exists(pathWordPad) || !File.Exists(Path.Combine(pathWordPad, "wordpad.exe")))
                    throw new FileNotFoundException("WordPad was removed from this Windows installation.");

                string backupPath = Path.Combine(CreateSessionRoot(), "Original");
                Directory.CreateDirectory(backupPath);
                
                CopyWordPadFiles(pathWordPad, backupPath);

                PeImage.ValidateWordPadExecutable(Path.Combine(backupPath, "wordpad.exe"));
                return backupPath;
            }
            catch (Exception ex)
            {
                AppLog.Error("Copy WordPad", ex);
                CleanTemporaryFiles();
                await ShowDialogAsync("WordPad unavailable", ex.Message);
                return "";
            }
        }

        private async Task<string> PrepareBundledWordPadAsync()
        {
            try
            {
                CleanTemporaryFiles();
                string sessionRoot = CreateSessionRoot();
                string backupPath = Path.Combine(sessionRoot, "Original");
                Directory.CreateDirectory(backupPath);

                WordPadStatusBar.Severity = InfoBarSeverity.Informational;
                WordPadStatusBar.Message = "Preparing bundled x86 WordPad...";

                var bundledFiles = new[]
                {
                    (Source: Path.Combine("resources", "wordpad.exe"), Destination: "wordpad.exe"),
                    (Source: Path.Combine("resources", "WordpadFilter.dll"), Destination: "WordpadFilter.dll")
                };

                foreach (var (sourceRelativePath, destinationRelativePath) in bundledFiles)
                {
                    string? sourceFile = ResolveBundledAssetPath(sourceRelativePath);
                    if (sourceFile == null)
                        throw new FileNotFoundException("A required bundled x86 WordPad file is missing.", sourceRelativePath);

                    string destinationFile = Path.Combine(backupPath, destinationRelativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
                    File.Copy(sourceFile, destinationFile, overwrite: true);
                }

                PeImage.ValidateWordPadExecutable(Path.Combine(backupPath, "wordpad.exe"));
                return backupPath;
            }
            catch (Exception ex)
            {
                AppLog.Error("Prepare bundled x86 WordPad", ex);
                CleanTemporaryFiles();
                await ShowDialogAsync("Bundled WordPad unavailable", ex.Message);
                WordPadStatusBar.Severity = InfoBarSeverity.Error;
                WordPadStatusBar.Message = "Bundled backup unavailable";
                return "";
            }
        }

        private void SetupWordPadPaths(string backupPath)
        {
            if (string.IsNullOrEmpty(_sessionRootPath) || !IsOwnedSessionRoot(_sessionRootPath) ||
                !PathGuard.IsWithin(_sessionRootPath, backupPath))
                throw new InvalidOperationException("The WordPad files are outside the private session directory.");

            _originalWordPadPath = backupPath;
            _workingRootPath = Path.Combine(_sessionRootPath, "Working");
            if (!ResetToOriginal())
                throw new IOException("Could not create an isolated working copy of WordPad.");
        }

        private async void WordPadSystemBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isActivityRunning || _isStarting || _isWindowClosed)
            {
                await ShowDialogAsync("Activity running", "Stop the current activity before changing the WordPad source.");
                return;
            }

            SetLoading(true);
            try
            {
                string backupPath = await CopyWordPadFolder();
                if (backupPath.Length == 0)
                    return;

                SetupWordPadPaths(backupPath);
                WordPadStatusBar.Severity = InfoBarSeverity.Success;
                WordPadStatusBar.Message = "Ready";
                ToggleStep2(true);

                await AutoFetchActivities();
            }
            catch (Exception ex)
            {
                AppLog.Error("Prepare system WordPad", ex);
                CleanTemporaryFiles();
                WordPadStatusBar.Severity = InfoBarSeverity.Error;
                WordPadStatusBar.Message = "WordPad setup failed";
                await ShowDialogAsync("WordPad setup failed", ex.Message);
            }
            finally
            {
                SetLoading(false);
            }
        }

        private async void WordPadBundledBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isActivityRunning || _isStarting || _isWindowClosed)
            {
                await ShowDialogAsync("Activity running", "Stop the current activity before changing the WordPad source.");
                return;
            }

            SetLoading(true);
            try
            {
                string backupPath = await PrepareBundledWordPadAsync();
                if (backupPath.Length == 0)
                    return;

                SetupWordPadPaths(backupPath);
                WordPadStatusBar.Severity = InfoBarSeverity.Success;
                WordPadStatusBar.Message = "Ready";
                ToggleStep2(true);
                await AutoFetchActivities();
            }
            catch (Exception ex)
            {
                AppLog.Error("Prepare bundled WordPad", ex);
                CleanTemporaryFiles();
                WordPadStatusBar.Severity = InfoBarSeverity.Error;
                WordPadStatusBar.Message = "WordPad setup failed";
                await ShowDialogAsync("WordPad setup failed", ex.Message);
            }
            finally
            {
                SetLoading(false);
            }
        }

        #endregion

        #region Step 2: Select Activity

        private void ActivityListCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isStarting && !_isActivityRunning && ActivityListCombo.SelectedIndex >= 0)
            {
                string selectedActivity = ActivityListCombo.SelectedItem?.ToString() ?? "";
                
                if (selectedActivity == "Custom Activity")
                {
                    FolderNameTxt.Text = "Accessories";
                    FileExeTxt.Text = "M1-Win64-Shipping";
                    ToggleStep21(true, isCustomActivity: true);
                }
                else if (_downloadedActivities != null)
                {
                    var activity = GetSelectedActivityItem();
                    if (activity != null)
                    {
                        FolderNameTxt.Text = activity.folder;
                        FileExeTxt.Text = Path.GetFileNameWithoutExtension(activity.exe);
                    }
                    ToggleStep21(true, isCustomActivity: false);
                }
                else
                {
                    ToggleStep21(true, isCustomActivity: false);
                }
            }
        }

        private ActivityItem? GetSelectedActivityItem()
        {
            string selectedActivity = ActivityListCombo.SelectedItem?.ToString() ?? "";
            return _downloadedActivities?.FirstOrDefault(activity =>
                string.Equals(GetActivityDisplayName(activity), selectedActivity, StringComparison.OrdinalIgnoreCase));
        }

        private static string GetActivityDisplayName(ActivityItem activity) =>
            activity.steamManifest == null ? activity.name : $"{activity.name} (SteamApps)";

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
            string selected = ActivityListCombo.SelectedItem?.ToString() ?? "";
            ActivityPopupList.Items.Clear();

            foreach (object item in ActivityListCombo.Items)
                ActivityPopupList.Items.Add(item?.ToString() ?? "");

            ActivityPopupList.SelectedItem = selected;
            if (ActivityListPopup.IsOpen && ActivityPopupList.SelectedItem != null)
                ActivityPopupList.ScrollIntoView(ActivityPopupList.SelectedItem);
        }

        private void ActivityPopupList_ItemClick(object sender, ItemClickEventArgs e)
        {
            CommitActivityPopupSelection(e.ClickedItem?.ToString());
        }

        private void ActivityPopupList_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                CommitActivityPopupSelection(ActivityPopupList.SelectedItem?.ToString());
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Escape)
            {
                ActivityListPopup.IsOpen = false;
                ActivityListButton.Focus(FocusState.Programmatic);
                e.Handled = true;
            }
        }

        private void CommitActivityPopupSelection(string? selectedActivity)
        {
            if (string.IsNullOrEmpty(selectedActivity))
                return;

            ActivityListCombo.SelectedItem = selectedActivity;
            ActivityListPopup.IsOpen = false;
            ActivityListButton.Focus(FocusState.Programmatic);
        }

        private async Task<bool> ConfirmSteamManifestSetupAsync(SteamManifestItem manifest)
        {
            var dialog = new ContentDialog
            {
                Title = "Steam manifest required",
                Content = $"This activity needs appmanifest_{manifest.appId}.acf. The mock executable and manifest will be created in a Steam library and kept there after the activity stops so Steam and Discord can detect them. Existing files will not be overwritten. If progress does not start, restart Steam and Discord. Continue?",
                PrimaryButtonText = "Create and start",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot,
                RequestedTheme = _isDarkTheme ? ElementTheme.Dark : ElementTheme.Light
            };

            return await ShowContentDialogAsync(dialog) == ContentDialogResult.Primary;
        }
        
        private async Task AutoFetchActivities()
        {
            if (_downloadedActivities != null && _downloadedActivities.Length > 0)
                return;

            try
            {
                LoadingRing.IsActive = true;
                var (activities, usedFallback) = await LoadActivitiesAsync();
                ApplyActivities(activities);
                if (usedFallback)
                    WordPadStatusBar.Message = "Offline activity list loaded";
            }
            catch (Exception ex)
            {
                AppLog.Error("Auto-fetch activity catalog", ex);
                if (!_isWindowClosed)
                {
                    WordPadStatusBar.Severity = InfoBarSeverity.Warning;
                    WordPadStatusBar.Message = $"Activity list unavailable: {ex.Message}";
                }
            }
            finally
            {
                LoadingRing.IsActive = false;
            }
        }

        private async void FetchActivitiesBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isActivityRunning || _isStarting || _isWindowClosed)
                return;

            try
            {
                FetchActivitiesBtn.IsEnabled = false;
                LoadingRing.IsActive = true;
                var (activities, usedFallback) = await LoadActivitiesAsync();
                ApplyActivities(activities);
                ActivitySearchBox.Text = "";
                if (usedFallback)
                    await ShowDialogAsync("Offline mode", "The online list could not be loaded. The bundled activity list was used instead.");
            }
            catch (Exception ex)
            {
                AppLog.Error("Fetch activity catalog", ex);
                if (!_isWindowClosed)
                    await ShowDialogAsync("Activity list error", ex.Message);
            }
            finally
            {
                LoadingRing.IsActive = false;
                if (!_isWindowClosed)
                    UpdateActivityControls();
            }
        }

        private async Task<(ActivityItem[] activities, bool usedFallback)> LoadActivitiesAsync()
        {
            Exception? onlineError = null;
            try
            {
                using var response = await activityHttpClient.GetAsync(githubActivityList, HttpCompletionOption.ResponseHeadersRead, _windowLifetime.Token);
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength is long length && length > 2 * 1024 * 1024)
                    throw new InvalidDataException("The activity catalog is larger than expected.");

                string content = await response.Content.ReadAsStringAsync(_windowLifetime.Token);
                if (content.Length > 2 * 1024 * 1024)
                    throw new InvalidDataException("The activity catalog is larger than expected.");
                return (ActivityRules.ParseActivities(content), false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidDataException)
            {
                onlineError = ex;
            }

            using Stream? bundledStream = typeof(MainWindow).Assembly.GetManifestResourceStream("DiscordActivityMockV2.ActivityList.json");
            if (bundledStream == null)
                throw new IOException("Could not download the catalog and no bundled catalog is available.", onlineError);

            using var reader = new StreamReader(bundledStream);
            return (ActivityRules.ParseActivities(await reader.ReadToEndAsync(_windowLifetime.Token)), true);
        }

        private void ApplyActivities(ActivityItem[] activities)
        {
            _downloadedActivities = activities;
            PopulateActivityList(ActivitySearchBox.Text);
            ActivitySearchBox.IsEnabled = !_isActivityRunning;
        }

        private void PopulateActivityList(string? searchFilter = null, bool resetSelectionWhenSearchCleared = false)
        {
            if (_downloadedActivities == null) return;

            string previousSelection = ActivityListCombo.SelectedItem?.ToString() ?? "";
            string normalizedSearch = searchFilter?.Trim() ?? "";

            ActivityListCombo.Items.Clear();

            ActivityItem[] filteredActivities = string.IsNullOrWhiteSpace(normalizedSearch)
                ? _downloadedActivities
                : _downloadedActivities.Where(activity =>
                    activity.name.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)).ToArray();

            foreach (var activity in filteredActivities)
            {
                ActivityListCombo.Items.Add(GetActivityDisplayName(activity));
            }

            // Always add Custom Activity at the end
            ActivityListCombo.Items.Add("Custom Activity");

            string selection = "Custom Activity";
            if (!string.IsNullOrWhiteSpace(normalizedSearch) && filteredActivities.Length > 0)
            {
                ActivityItem closestMatch = filteredActivities.FirstOrDefault(activity =>
                    string.Equals(activity.name, normalizedSearch, StringComparison.OrdinalIgnoreCase))
                    ?? filteredActivities.FirstOrDefault(activity =>
                        activity.name.StartsWith(normalizedSearch, StringComparison.OrdinalIgnoreCase))
                    ?? filteredActivities[0];
                selection = GetActivityDisplayName(closestMatch);
            }
            else if (string.IsNullOrWhiteSpace(normalizedSearch) && !resetSelectionWhenSearchCleared &&
                     ActivityListCombo.Items.Contains(previousSelection))
            {
                selection = previousSelection;
            }

            ActivityListCombo.SelectedItem = selection;
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
            if (_isStarting || _isActivityRunning)
                return;

            _isStarting = true;
            UpdateActivityControls();
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
                    await ShowDialogAsync("WordPad required", "Prepare a valid WordPad copy before starting an activity.");
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
                            throw new ArgumentException("The executable name for a Steam manifest activity must match its catalog entry.");
                    }
                }
                catch (ArgumentException ex)
                {
                    await ShowDialogAsync("Invalid activity", ex.Message);
                    return;
                }

                string normalizedFolderName = Path.GetRelativePath(_workingRootPath, newFolderPath);
                bool needsReset = !string.IsNullOrEmpty(_lastFolderName) &&
                                  (!string.Equals(_lastFolderName, normalizedFolderName, StringComparison.OrdinalIgnoreCase) ||
                                   !string.Equals(_lastExeName, targetExeName, StringComparison.OrdinalIgnoreCase));
                if (needsReset && !ResetToOriginal())
                {
                    await ShowDialogAsync("Setup error", "Could not restore the isolated WordPad copy. Reload the WordPad source and try again.");
                    return;
                }

                if (!PathGuard.IsWithin(_workingRootPath, _currentFolderPath))
                    throw new InvalidOperationException("The current working folder is outside the private session directory.");

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
                    WordPadStatusBar.Message = "Steam manifest ready";
                }
                else
                {
                    string currentExeName = Path.GetFileNameWithoutExtension(_currentExePath);
                    string currentExeDirectory = Path.GetDirectoryName(_currentExePath)!;
                    if (!currentExeName.Equals(targetExeName, StringComparison.OrdinalIgnoreCase))
                    {
                        string newExePath = Path.Combine(currentExeDirectory, targetExeName + ".exe");
                        if (File.Exists(newExePath))
                            throw new IOException("The target executable already exists in this isolated working folder.");
                        RenameWordPadMuiFiles(_currentFolderPath, Path.GetFileName(_currentExePath), targetExeName);
                        File.Move(_currentExePath, newExePath);
                        _currentExePath = newExePath;
                    }

                    string currentFolderPath = Path.GetFullPath(_currentFolderPath);
                    if (!string.Equals(currentFolderPath, newFolderPath, StringComparison.OrdinalIgnoreCase))
                    {
                        if (Directory.Exists(newFolderPath) || File.Exists(newFolderPath))
                            throw new IOException("The requested activity folder already exists in the private session directory.");

                        if (PathGuard.IsWithin(currentFolderPath, newFolderPath))
                            throw new ArgumentException("The activity folder cannot be inside the temporary WordPad folder.");

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
                    throw new InvalidOperationException("Windows did not start the activity process.");
                }

                _runningProcess = process;
                _isActivityRunning = true;
                process.EnableRaisingEvents = true;

                _hideCancellation?.Cancel();
                _hideCancellation?.Dispose();
                _hideCancellation = new CancellationTokenSource();
                if (_hideEnabled)
                    _ = HideProcessAfterDelayAsync(process, sessionId, _hideDelaySeconds, _hideCancellation.Token);

                _activityStartTime = DateTime.Now;
                _activityTimer?.Start();
                if (_autoStopEnabled && _autoStopTimer != null)
                {
                    _autoStopTimer.Interval = TimeSpan.FromMinutes(_autoStopMinutes);
                    _autoStopTimer.Start();
                }

                UpdateActivityControls();
                if (process.HasExited)
                    DispatcherQueue.TryEnqueue(() => HandleProcessExited(process, sessionId));
            }
            catch (Exception ex)
            {
                AppLog.Error("Start activity", ex);
                bool stopped = !_isActivityRunning || StopRunningProcess();
                if (stopped)
                {
                    _activityTimer?.Stop();
                    _autoStopTimer?.Stop();
                    TimerText.Text = "00:00:00";
                    ResetToOriginal();
                }
                else
                {
                    WordPadStatusBar.Severity = InfoBarSeverity.Error;
                    WordPadStatusBar.Message = "Windows could not stop the partially started process.";
                }
                await ShowDialogAsync("Could not start activity", ex.Message);
            }
            finally
            {
                _isStarting = false;
                UpdateActivityControls();
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
            _activityTimer?.Stop();
            _autoStopTimer?.Stop();
            process.Dispose();
            UpdateActivityControls();
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
                _activityTimer?.Stop();
                _autoStopTimer?.Stop();
                TimerText.Text = "00:00:00";
            }
            else
            {
                WordPadStatusBar.Severity = InfoBarSeverity.Error;
                WordPadStatusBar.Message = "Windows could not stop the activity process. Try again.";
            }
            UpdateActivityControls();
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
            
            if (_autoStopEnabled)
            {
                TimerStatusText.Text = $"Timer: {_autoStopMinutes}m";
            }
            else
            {
                TimerStatusText.Text = "Timer: Off";
            }
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
            
            if (_hideEnabled)
            {
                HideStatusText.Text = $"Hide: {_hideDelaySeconds}s";
            }
            else
            {
                HideStatusText.Text = "Hide: Off";
            }
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
                RequestedTheme = _isDarkTheme ? ElementTheme.Dark : ElementTheme.Light
            };
            if (await ShowContentDialogAsync(dialog) == ContentDialogResult.Primary)
                await CheckForUpdatesAsync(showResultWhenCurrent: true);
        }

        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppLog.Error("Open external URL", ex);
            }
        }

        #endregion
    }
}
