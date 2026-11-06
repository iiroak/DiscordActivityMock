using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;

namespace DiscordActivityMockV2
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;

        public App()
        {
            InitializeComponent();

            // Record crashes in app.log; handling stays the same.
            UnhandledException += (_, e) => AppLog.Error("Unhandled UI exception", e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                if (e.ExceptionObject is Exception exception)
                    AppLog.Error("Unhandled exception", exception);
            };
            TaskScheduler.UnobservedTaskException += (_, e) => AppLog.Error("Unobserved task exception", e.Exception);
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            if (UpdateChecker.TryRedirectPortableUpdate())
            {
                Exit();
                return;
            }

            _window = new MainWindow();
            _window.Activate();
        }
    }
}
