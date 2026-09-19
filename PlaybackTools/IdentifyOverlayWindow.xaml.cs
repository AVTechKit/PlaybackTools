using System.Windows;

namespace PlaybackTools
{
    /// <summary>Brief full-screen badge showing a display's number, mirroring the
    /// "Identify Displays" feature in Windows' own display settings - lets the user
    /// match dropdown entries to physical monitors before committing a change.
    /// Positioned the same way DisplayWindow is (Screen.Bounds -> Left/Top/Width/Height),
    /// which lines up correctly given the app's PerMonitorV2 DPI awareness.</summary>
    public partial class IdentifyOverlayWindow : Window
    {
        public IdentifyOverlayWindow(System.Windows.Forms.Screen screen, int number)
        {
            InitializeComponent();

            Left = screen.Bounds.Left;
            Top = screen.Bounds.Top;
            Width = screen.Bounds.Width;
            Height = screen.Bounds.Height;

            NumberText.Text = number.ToString();
        }
    }
}