using System.Configuration;
using System.Data;
using System.Windows;

namespace PlaybackTools
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var splash = new SplashWindow();
            splash.Show();

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var mainWindow = new MainWindow();
            MainWindow = mainWindow; // StartupUri normally sets this automatically - we do it explicitly now
            mainWindow.Show();

            const int minimumSplashMs = 900;
            int remaining = minimumSplashMs - (int)stopwatch.ElapsedMilliseconds;

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = System.TimeSpan.FromMilliseconds(System.Math.Max(0, remaining))
            };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                splash.Close();
            };
            timer.Start();
        }
    }

}
