using System;
using System.Windows;
using OpenTK.Graphics.OpenGL;
using OpenTK.Wpf;
using PlaybackTools.Playback;
using System.Windows.Media.Imaging;
using PlaybackTools.ViewModels;
using System.Windows.Media.Animation;

namespace PlaybackTools
{
    public partial class DisplayWindow : Window
    {
        private readonly PlaybackEngine _engine;
        private readonly MainViewModel _viewModel;

        private bool _closeAllowed;

        public GLWpfControl ViewA => GlViewA;
        public GLWpfControl ViewB => GlViewB;

        public DisplayWindow(PlaybackEngine engine, MainViewModel viewModel, System.Windows.Forms.Screen targetScreen)
        {
            _engine = engine;
            _viewModel = viewModel;
            InitializeComponent();

            // Position before showing, to avoid a visible flash on the wrong monitor.
            MoveToScreen(targetScreen);

            var settings = new GLWpfControlSettings { MajorVersion = 3, MinorVersion = 3 };
            GlViewA.Start(settings);
            GlViewB.Start(settings);

            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.BackgroundImagePath))
                    RefreshBackgroundImage();
            };
            RefreshBackgroundImage(); // in case an image was already loaded before this window existed (Show Building Mode)
        }

        public void AllowClose() => _closeAllowed = true;

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_closeAllowed)
            {
                e.Cancel = true;
                return;
            }
            base.OnClosing(e);
        }

        private void RefreshBackgroundImage()
        {
            if (_viewModel.BackgroundImagePath == null)
            {
                BackgroundImage.Source = null;
                return;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(_viewModel.BackgroundImagePath);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();

            BackgroundImage.Source = bitmap;
        }

        public void MoveToScreen(System.Windows.Forms.Screen targetScreen)
        {
            Left = targetScreen.Bounds.Left;
            Top = targetScreen.Bounds.Top;
            Width = targetScreen.Bounds.Width;
            Height = targetScreen.Bounds.Height;
        }

        private void GlViewA_Render(TimeSpan delta) => RenderPlayer(0, GlViewA);
        private void GlViewB_Render(TimeSpan delta) => RenderPlayer(1, GlViewB);

        private void RenderPlayer(int index, GLWpfControl view)
        {
            _engine.EnsureInitialized(index);

            if (!_engine.HasActiveClip(index))
            {
                GL.ClearColor(0f, 0f, 0f, 1f);
                GL.Clear(ClearBufferMask.ColorBufferBit);
                return;
            }

            GL.GetInteger(GetPName.FramebufferBinding, out int fbo);
            _engine.RenderInto(index, fbo, (int)view.ActualWidth, (int)view.ActualHeight);
        }

        public void SetBackgroundVisible(bool showBackground)
        {
            BackgroundImage.BeginAnimation(OpacityProperty, null);

            if (showBackground)
            {
                BackgroundImage.Opacity = 1;
                BackgroundImage.Visibility = Visibility.Visible;
            }
            else
            {
                BackgroundImage.Visibility = Visibility.Collapsed;
            }
        }

        public void FadeBackgroundOut(double durationMs)
        {
            var fade = new DoubleAnimation(BackgroundImage.Opacity, 0, TimeSpan.FromMilliseconds(durationMs));
            fade.Completed += (s, e) =>
            {
                BackgroundImage.BeginAnimation(OpacityProperty, null);
                BackgroundImage.Opacity = 0;
                BackgroundImage.Visibility = Visibility.Collapsed;
            };
            BackgroundImage.BeginAnimation(OpacityProperty, fade);
        }
        public void FadeBackgroundIn(double durationMs)
        {
            BackgroundImage.BeginAnimation(OpacityProperty, null);
            BackgroundImage.Opacity = 0;
            BackgroundImage.Visibility = Visibility.Visible;

            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(durationMs));
            BackgroundImage.BeginAnimation(OpacityProperty, fade);
        }
    }
}
