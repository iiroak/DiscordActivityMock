using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace DiscordActivityMockV2
{
    public class ActivityItem
    {
        public string name { get; set; } = "";
        public string folder { get; set; } = "";
        public string exe { get; set; } = "";
    }
    
    public class AppSettings
    {
        public bool HideEnabled { get; set; } = true;
        public int HideDelaySeconds { get; set; } = 15;
        public bool AutoStopEnabled { get; set; } = false;
        public int AutoStopMinutes { get; set; } = 30;
        public bool DontShowStartWarning { get; set; } = false;
        public bool IsDarkTheme { get; set; } = true;
    }

    [JsonSerializable(typeof(ActivityItem[]))]
    [JsonSerializable(typeof(AppSettings))]
    [JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, WriteIndented = true)]
    internal partial class AppJsonContext : JsonSerializerContext
    {
    }

    public sealed partial class MainWindow : Window
    {
        // Win32 API for hiding windows
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        
        private const int SW_HIDE = 0;
        
        private static readonly string settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DiscordActivityMock", "settings.json");

        private static readonly string githubActivityList = "https://raw.githubusercontent.com/iiroak/DiscordActivityMock/refs/heads/main/resources/ActivityList.json";
        private static readonly string githubWordPad = "https://raw.githubusercontent.com/iiroak/DiscordActivityMock/refs/heads/main/resources/WordPad.zip";
        
        private string? _originalWordPadPath;  // Path to original WordPad backup
        private string? _currentFolderPath;    // Current working folder path
        private string? _currentExePath;       // Current executable path
        private string _lastFolderName = "";   // Last folder name used
        private string _lastExeName = "";      // Last exe name used
        private ActivityItem[]? _downloadedActivities;
        private Process? _runningProcess;
        private bool _isActivityRunning = false;
        private bool _isCustomActivity = false;
        
        // Timer for tracking activity duration
        private DispatcherTimer? _activityTimer;
        private DateTime _activityStartTime;
        
        // Auto-stop timer
        private DispatcherTimer? _autoStopTimer;
        private bool _autoStopEnabled = false;
        private int _autoStopMinutes = 30;
        
        // Hide settings
        private bool _hideEnabled = true;
        private int _hideDelaySeconds = 15;
        
        // Settings
        private bool _dontShowStartWarning = false;
        
        // Window management
        private AppWindow? _appWindow;
        private IntPtr _hwnd;
        private bool _isDarkTheme = true;

        public MainWindow()
        {
            InitializeComponent();
            
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
            
            // Set window size and get AppWindow
            _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);
            _appWindow.Resize(new SizeInt32(900, 440));
            
            // Setup custom titlebar (fully custom, hide system buttons)
            SetupCustomTitleBar();
            
            // Initialize UI
            ActivityListCombo.Items.Add("Custom Activity");
            ActivityListCombo.SelectedIndex = 0;
            
            // Apply loaded settings to UI
            ApplyLoadedSettings();
            
            // Apply localization to UI
            ApplyLocalization();
            
            // Check for updates on startup
            _ = CheckForUpdatesAsync();
        }
        
        private async Task CheckForUpdatesAsync()
        {
            try
            {
                var (hasUpdate, newVersion, downloadUrl, releaseUrl) = await UpdateChecker.CheckForUpdateAsync();
                
                if (hasUpdate && newVersion != null && releaseUrl != null)
                {
                    var dialog = new ContentDialog
                    {
                        Title = Localization.Get("UpdateAvailable"),
                        Content = Localization.Get("UpdateMessage", newVersion),
                        PrimaryButtonText = Localization.Get("Download"),
                        CloseButtonText = Localization.Get("Later"),
                        DefaultButton = ContentDialogButton.Primary,
                        XamlRoot = this.Content.XamlRoot,
                        RequestedTheme = _isDarkTheme ? ElementTheme.Dark : ElementTheme.Light
                    };
                    
                    var result = await dialog.ShowAsync();
                    
                    if (result == ContentDialogResult.Primary)
                    {
                        UpdateChecker.OpenReleasePage(releaseUrl);
                    }
                }
            }
            catch { }
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
            DownloadBackupText.Text = Localization.Get("DownloadBackup");
            StartBtnText.Text = Localization.Get("Start");
            StopBtnText.Text = Localization.Get("Stop");
            
            // Search placeholder
            ActivitySearchBox.PlaceholderText = Localization.Get("Search");
            
            // Status bar
            WordPadStatusBar.Title = Localization.Get("Status");
            WordPadStatusBar.Message = Localization.Get("SelectOptionAbove");
            
            // Footer
            GitHubBtnText.Text = Localization.Get("GitHub");
            DiscordBtnText.Text = Localization.Get("Discord");
            AboutBtnText.Text = Localization.Get("About");
            MadeWithText.Text = Localization.Get("MadeWith");
            ByText.Text = Localization.Get("By");
            
            // Tooltips
            InfoBtn.SetValue(ToolTipService.ToolTipProperty, Localization.Get("About"));
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            StopRunningProcess();
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
            WordPadGitHubBtn.IsEnabled = !isLoading;
        }

        private void UpdateActivityControls()
        {
            bool wordPadReady = !string.IsNullOrEmpty(_currentExePath);
            StartActivityBtn.IsEnabled = !_isActivityRunning && wordPadReady;
            StopActivityBtn.IsEnabled = _isActivityRunning;
            FolderNameTxt.IsEnabled = !_isActivityRunning && _isCustomActivity;
            FileExeTxt.IsEnabled = !_isActivityRunning && _isCustomActivity;
            ClearBtn.IsEnabled = !_isActivityRunning && _isCustomActivity;
            ActivityListCombo.IsEnabled = !_isActivityRunning;
            HideSettingsBtn.IsEnabled = wordPadReady && !_isActivityRunning;
            TimerSettingsBtn.IsEnabled = wordPadReady && !_isActivityRunning;
            
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
            // Time's up - stop the activity
            _autoStopTimer?.Stop();
            StopRunningProcess();
            _activityTimer?.Stop();
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
                        _hideDelaySeconds = settings.HideDelaySeconds;
                        _autoStopEnabled = settings.AutoStopEnabled;
                        _autoStopMinutes = settings.AutoStopMinutes;
                        _dontShowStartWarning = settings.DontShowStartWarning;
                        _isDarkTheme = settings.IsDarkTheme;
                    }
                }
            }
            catch { }
        }
        
        private void SaveSettings()
        {
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
                File.WriteAllText(settingsPath, json);
            }
            catch { }
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

        private void StopRunningProcess()
        {
            if (_runningProcess != null && !_runningProcess.HasExited)
            {
                try
                {
                    _runningProcess.Kill();
                    _runningProcess.Dispose();
                }
                catch { }
            }
            _runningProcess = null;
            _isActivityRunning = false;
        }
        private void CleanTemporaryFiles()
        {
            try
            {
                // Clean original backup
                if (!string.IsNullOrEmpty(_originalWordPadPath) && Directory.Exists(_originalWordPadPath))
                {
                    Directory.Delete(_originalWordPadPath, true);
                }
                
                // Clean current working folder
                if (!string.IsNullOrEmpty(_currentFolderPath) && Directory.Exists(_currentFolderPath))
                {
                    Directory.Delete(_currentFolderPath, true);
                }
            }
            catch
            {
                // Silently ignore cleanup errors
            }
            finally
            {
                _originalWordPadPath = null;
                _currentFolderPath = null;
                _currentExePath = null;
            }
        }

        /// <summary>
        /// Resets the working folder to original WordPad state for re-renaming
        /// </summary>
        private bool ResetToOriginal()
        {
            if (string.IsNullOrEmpty(_originalWordPadPath) || !Directory.Exists(_originalWordPadPath))
                return false;

            try
            {
                // Delete current working folder if it exists and is different
                if (!string.IsNullOrEmpty(_currentFolderPath) && 
                    Directory.Exists(_currentFolderPath) && 
                    _currentFolderPath != _originalWordPadPath)
                {
                    Directory.Delete(_currentFolderPath, true);
                }

                // Create a fresh copy from original
                string workingPath = Path.Combine(Path.GetTempPath(), "WordPadWorking_" + Guid.NewGuid().ToString("N")[..8]);
                Directory.CreateDirectory(workingPath);

                foreach (var file in Directory.GetFiles(_originalWordPadPath))
                {
                    string destFile = Path.Combine(workingPath, Path.GetFileName(file));
                    File.Copy(file, destFile, true);
                }

                _currentFolderPath = workingPath;
                _currentExePath = Path.Combine(workingPath, "wordpad.exe");
                _lastFolderName = "";
                _lastExeName = "";

                return true;
            }
            catch
            {
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
            await dialog.ShowAsync();
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
            
            var result = await dialog.ShowAsync();
            
            if (dontShowCheckBox.IsChecked == true)
            {
                _dontShowStartWarning = true;
                SaveSettings();
            }
            
            return result == ContentDialogResult.Primary;
        }

        #endregion

        #region Step 1: Copy or Download WordPad

        private string CopyWordPadFolder()
        {
            string pathWordPad = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Windows NT", "Accessories");
            string backupPath = Path.Combine(Path.GetTempPath(), "WordPadOriginal_" + Guid.NewGuid().ToString("N")[..8]);
            
            try
            {
                CleanTemporaryFiles();
                Directory.CreateDirectory(backupPath);
                
                foreach (var file in Directory.GetFiles(pathWordPad))
                {
                    string destFile = Path.Combine(backupPath, Path.GetFileName(file));
                    File.Copy(file, destFile, true);
                }

                // Verify wordpad.exe exists
                if (!File.Exists(Path.Combine(backupPath, "wordpad.exe")))
                {
                    Directory.Delete(backupPath, true);
                    _ = ShowDialogAsync("Error", "WordPad executable not found in system folder.");
                    return "";
                }
            }
            catch (Exception ex)
            {
                _ = ShowDialogAsync("Error", $"Error copying WordPad folder: {ex.Message}");
                return "";
            }
            
            return backupPath;
        }

        private async Task<string> DownloadAndExtractWordPad()
        {
            string backupPath = Path.Combine(Path.GetTempPath(), "WordPadOriginal_" + Guid.NewGuid().ToString("N")[..8]);
            string zipPath = Path.Combine(Path.GetTempPath(), "WordPad_" + Guid.NewGuid().ToString("N")[..8] + ".zip");
            
            try
            {
                CleanTemporaryFiles();
                
                WordPadStatusBar.Severity = InfoBarSeverity.Informational;
                WordPadStatusBar.Message = "Downloading...";

                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(2);
                    byte[] zipData = await client.GetByteArrayAsync(githubWordPad);
                    await File.WriteAllBytesAsync(zipPath, zipData);
                }

                WordPadStatusBar.Message = "Preparing files...";
                Directory.CreateDirectory(backupPath);
                ZipFile.ExtractToDirectory(zipPath, backupPath);
                File.Delete(zipPath);

                string wordpadPath = Path.Combine(backupPath, "wordpad.exe");
                if (!File.Exists(wordpadPath))
                {
                    await ShowDialogAsync("Error", "WordPad executable not found in downloaded files.");
                    return "";
                }
                
                return backupPath;
            }
            catch (HttpRequestException ex)
            {
                await ShowDialogAsync("Download Error", $"Error downloading WordPad: {ex.Message}");
                WordPadStatusBar.Severity = InfoBarSeverity.Error;
                WordPadStatusBar.Message = "Download failed";
            }
            catch (Exception ex)
            {
                await ShowDialogAsync("Extraction Error", $"Error extracting WordPad: {ex.Message}");
                WordPadStatusBar.Severity = InfoBarSeverity.Error;
                WordPadStatusBar.Message = "Failed to extract";
            }
            finally
            {
                if (File.Exists(zipPath))
                {
                    try { File.Delete(zipPath); } catch { }
                }
            }
            
            return "";
        }

        private void SetupWordPadPaths(string backupPath)
        {
            _originalWordPadPath = backupPath;
            
            // Create working copy
            string workingPath = Path.Combine(Path.GetTempPath(), "WordPadWorking_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(workingPath);

            foreach (var file in Directory.GetFiles(backupPath))
            {
                string destFile = Path.Combine(workingPath, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            _currentFolderPath = workingPath;
            _currentExePath = Path.Combine(workingPath, "wordpad.exe");
        }

        private void WordPadSystemBtn_Click(object sender, RoutedEventArgs e)
        {
            SetLoading(true);
            
            string backupPath = CopyWordPadFolder();
            if (backupPath != "")
            {
                SetupWordPadPaths(backupPath);
                WordPadStatusBar.Severity = InfoBarSeverity.Success;
                WordPadStatusBar.Message = "Ready";
                ToggleStep2(true);
                
                // Auto-fetch activities
                _ = AutoFetchActivities();
            }
            
            SetLoading(false);
        }

        private async void WordPadGitHubBtn_Click(object sender, RoutedEventArgs e)
        {
            SetLoading(true);
            
            string backupPath = await DownloadAndExtractWordPad();
            if (backupPath != "")
            {
                SetupWordPadPaths(backupPath);
                WordPadStatusBar.Severity = InfoBarSeverity.Success;
                WordPadStatusBar.Message = "Ready";
                ToggleStep2(true);
                
                // Auto-fetch activities
                await AutoFetchActivities();
            }
            
            SetLoading(false);
        }

        #endregion

        #region Step 2: Select Activity

        private void ActivityListCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ActivityListCombo.SelectedIndex >= 0)
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
                    var activity = _downloadedActivities.FirstOrDefault(a => a.name == selectedActivity);
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
        
        private async Task AutoFetchActivities()
        {
            // Auto-fetch activities silently
            if (_downloadedActivities != null && _downloadedActivities.Length > 0)
                return; // Already fetched
                
            try
            {
                LoadingRing.IsActive = true;
                
                using HttpClient client = new();
                client.Timeout = TimeSpan.FromSeconds(30);
                string jsonContent = await client.GetStringAsync(githubActivityList);
                var activities = JsonSerializer.Deserialize(jsonContent, AppJsonContext.Default.ActivityItemArray);
                
                if (activities != null && activities.Length > 0)
                {
                    _downloadedActivities = activities.OrderBy(a => a.name, StringComparer.OrdinalIgnoreCase).ToArray();
                    PopulateActivityList();
                    ActivitySearchBox.IsEnabled = true;
                }
            }
            catch
            {
                // Silently ignore auto-fetch errors
            }
            finally
            {
                LoadingRing.IsActive = false;
            }
        }

        private async void FetchActivitiesBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                FetchActivitiesBtn.IsEnabled = false;
                LoadingRing.IsActive = true;
                
                using HttpClient client = new();
                client.Timeout = TimeSpan.FromMinutes(1);
                string jsonContent = await client.GetStringAsync(githubActivityList);
                var activities = JsonSerializer.Deserialize(jsonContent, AppJsonContext.Default.ActivityItemArray);
                
                if (activities != null && activities.Length > 0)
                {
                    // Sort activities alphabetically by name
                    _downloadedActivities = activities.OrderBy(a => a.name, StringComparer.OrdinalIgnoreCase).ToArray();
                    
                    PopulateActivityList();
                    ActivitySearchBox.IsEnabled = true;
                    ActivitySearchBox.Text = "";
                }
                else
                {
                    await ShowDialogAsync("Warning", "No activities found in the list.");
                }
            }
            catch (HttpRequestException ex)
            {
                await ShowDialogAsync("Download Error", $"Error downloading activity list: {ex.Message}");
            }
            catch (JsonException ex)
            {
                await ShowDialogAsync("Parse Error", $"Error parsing activity list JSON: {ex.Message}");
            }
            catch (Exception ex)
            {
                await ShowDialogAsync("Error", $"Unexpected error: {ex.Message}");
            }
            finally
            {
                FetchActivitiesBtn.IsEnabled = true;
                LoadingRing.IsActive = false;
            }
        }

        private void PopulateActivityList(string? searchFilter = null)
        {
            if (_downloadedActivities == null) return;

            ActivityListCombo.Items.Clear();
            
            var filteredActivities = _downloadedActivities.AsEnumerable();
            
            if (!string.IsNullOrWhiteSpace(searchFilter))
            {
                filteredActivities = filteredActivities.Where(a => 
                    a.name.Contains(searchFilter, StringComparison.OrdinalIgnoreCase));
            }
            
            foreach (var activity in filteredActivities)
            {
                ActivityListCombo.Items.Add(activity.name);
            }
            
            // Always add Custom Activity at the end
            ActivityListCombo.Items.Add("Custom Activity");
            
            if (ActivityListCombo.Items.Count > 0)
            {
                ActivityListCombo.SelectedIndex = 0;
            }
        }

        private void ActivitySearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            PopulateActivityList(ActivitySearchBox.Text);
        }

        #endregion

        #region Step 3: Run Activity

        private async void StartActivityBtn_Click(object sender, RoutedEventArgs e)
        {
            // Show warning dialog if not disabled
            if (!_dontShowStartWarning)
            {
                var result = await ShowStartWarningDialog();
                if (!result)
                    return;
            }
            
            string targetFolderName = FolderNameTxt.Text.Trim();
            string targetExeName = FileExeTxt.Text.Trim();

            if (string.IsNullOrEmpty(targetFolderName) || string.IsNullOrEmpty(targetExeName))
            {
                await ShowDialogAsync("Error", "Please enter both folder name and executable name.");
                return;
            }

            // Check if we need to reset (different activity selected)
            bool needsReset = !string.IsNullOrEmpty(_lastFolderName) && 
                              (_lastFolderName != targetFolderName || _lastExeName != targetExeName);

            if (needsReset)
            {
                if (!ResetToOriginal())
                {
                    await ShowDialogAsync("Error", "Failed to reset WordPad files. Please reload WordPad.");
                    return;
                }
            }

            if (string.IsNullOrEmpty(_currentExePath) || !File.Exists(_currentExePath))
            {
                await ShowDialogAsync("Error", "Executable not found. Please copy WordPad again.");
                return;
            }

            if (string.IsNullOrEmpty(_currentFolderPath) || !Directory.Exists(_currentFolderPath))
            {
                await ShowDialogAsync("Error", "Folder not found. Please copy WordPad again.");
                return;
            }

            try
            {
                StartActivityBtn.IsEnabled = false;
                
                string parentDir = Path.GetDirectoryName(_currentFolderPath)!;
                string newFolderPath = Path.Combine(parentDir, targetFolderName);
                string currentExeName = Path.GetFileNameWithoutExtension(_currentExePath);

                // Clean up any existing folder with the target name (from previous runs)
                if (Directory.Exists(newFolderPath) && newFolderPath != _currentFolderPath)
                {
                    try
                    {
                        Directory.Delete(newFolderPath, true);
                    }
                    catch
                    {
                        // If can't delete, use unique name
                        newFolderPath = Path.Combine(parentDir, targetFolderName + "_" + Guid.NewGuid().ToString("N")[..4]);
                    }
                }

                // Rename the executable file if needed
                if (!currentExeName.Equals(targetExeName, StringComparison.OrdinalIgnoreCase))
                {
                    string newExePath = Path.Combine(Path.GetDirectoryName(_currentExePath)!, targetExeName + ".exe");
                    
                    // Delete existing file if present
                    if (File.Exists(newExePath))
                    {
                        File.Delete(newExePath);
                    }
                    
                    File.Move(_currentExePath, newExePath);
                    _currentExePath = newExePath;
                }

                // Rename the folder if needed
                string currentFolderName = Path.GetFileName(_currentFolderPath);
                if (!currentFolderName.Equals(targetFolderName, StringComparison.OrdinalIgnoreCase) && 
                    _currentFolderPath != newFolderPath)
                {
                    Directory.Move(_currentFolderPath, newFolderPath);
                    _currentFolderPath = newFolderPath;
                    _currentExePath = Path.Combine(newFolderPath, Path.GetFileName(_currentExePath));
                }

                // Store last used names
                _lastFolderName = targetFolderName;
                _lastExeName = targetExeName;

                // Start the process hidden
                var processInfo = new ProcessStartInfo
                {
                    FileName = _currentExePath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                _runningProcess = Process.Start(processInfo);
                
                if (_runningProcess != null)
                {
                    _isActivityRunning = true;
                    
                    // Hide the WordPad window if enabled
                    if (_hideEnabled)
                    {
                        int delaySeconds = _hideDelaySeconds;
                        _ = Task.Run(async () =>
                        {
                            // Wait configured seconds for Discord to detect the activity before hiding
                            await Task.Delay(delaySeconds * 1000);
                            try
                            {
                                if (_runningProcess != null && !_runningProcess.HasExited)
                                {
                                    IntPtr hwnd = _runningProcess.MainWindowHandle;
                                    if (hwnd != IntPtr.Zero)
                                    {
                                        ShowWindow(hwnd, SW_HIDE);
                                    }
                                }
                            }
                            catch { }
                        });
                    }

                    // Monitor process exit
                    _runningProcess.EnableRaisingEvents = true;
                    _runningProcess.Exited += (s, args) =>
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            _isActivityRunning = false;
                            _activityTimer?.Stop();
                            _autoStopTimer?.Stop();
                            UpdateActivityControls();
                        });
                    };
                    
                    // Start the timer
                    _activityStartTime = DateTime.Now;
                    _activityTimer?.Start();
                    
                    // Start auto-stop timer if enabled
                    if (_autoStopEnabled && _autoStopTimer != null)
                    {
                        _autoStopTimer.Interval = TimeSpan.FromMinutes(_autoStopMinutes);
                        _autoStopTimer.Start();
                    }

                    UpdateActivityControls();
                }
                else
                {
                    await ShowDialogAsync("Error", "Failed to start the process.");
                    StartActivityBtn.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                await ShowDialogAsync("Error", $"Error during activity setup: {ex.Message}");
                StartActivityBtn.IsEnabled = true;
            }
        }

        private void StopActivityBtn_Click(object sender, RoutedEventArgs e)
        {
            StopRunningProcess();
            _activityTimer?.Stop();
            _autoStopTimer?.Stop();
            TimerText.Text = "00:00:00";
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
            if (!double.IsNaN(args.NewValue))
            {
                _autoStopMinutes = (int)args.NewValue;
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
            if (!double.IsNaN(args.NewValue))
            {
                _hideDelaySeconds = (int)args.NewValue;
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
        private void DiscordBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://discord.gg/U253PPeMMY");
        }

        private async void InfoBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = Localization.Get("AboutTitle"),
                Content = Localization.Get("AboutContent"),
                CloseButtonText = Localization.Get("OK"),
                XamlRoot = this.Content.XamlRoot,
                RequestedTheme = _isDarkTheme ? ElementTheme.Dark : ElementTheme.Light
            };
            await dialog.ShowAsync();
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
            catch { }
        }

        #endregion
    }
}
