using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using NAudio.CoreAudioApi;
using PlaybackTools.ViewModels;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace PlaybackTools
{

    public partial class SettingsWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public event Action<System.Windows.Forms.Screen>? DisplayChangeRequested;

        public event Action<string?>? AudioDeviceChangeRequested;

        public event Action? NewShowRequested;
        public event Action<string>? SaveShowRequested;
        public event Action<string>? LoadShowRequested;

        public SettingsWindow(MainViewModel viewModel)
        {
            _viewModel = viewModel;
            InitializeComponent();

            VersionTextBlock.Text = $"Version {GetVersionString()}";
            DefaultEndActionComboBox.ItemsSource = new[]
             {
                EndAction.LoopCut, EndAction.LoopFade, EndAction.PlayNext, EndAction.PauseOnLastFrame, EndAction.StopToBlack
            };

            if (_viewModel.DefaultEndAction == EndAction.PlaySelected)
                _viewModel.DefaultEndAction = EndAction.PauseOnLastFrame;

            DefaultEndActionComboBox.SelectedItem = _viewModel.DefaultEndAction;
            PopulateDisplayComboBox();
            PopulateAudioDeviceComboBox(_viewModel.SelectedAudioDeviceId);
        }

        private static string GetVersionString()
        {
            var infoVersion = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            return !string.IsNullOrEmpty(infoVersion) ? infoVersion : "dev";
        }

        private void PopulateDisplayComboBox()
        {
            var screens = System.Windows.Forms.Screen.AllScreens;
            var options = new List<DisplayOption>();
            for (int i = 0; i < screens.Length; i++)
                options.Add(new DisplayOption(screens[i], i + 1));

            DisplayComboBox.ItemsSource = options;

            var current = options.FirstOrDefault(o => o.Screen.DeviceName == _viewModel.SelectedDisplayDeviceName);
            DisplayComboBox.SelectedItem = current ?? options.FirstOrDefault(o => o.Screen.Primary) ?? options.FirstOrDefault();
        }

        private void PopulateAudioDeviceComboBox(string? preferredId)
        {
            var options = new List<AudioDeviceOption> { new AudioDeviceOption(null, "System Default") };

            try
            {
                using var enumerator = new MMDeviceEnumerator();

                string? defaultId = null;
                try
                {
                    using var defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    defaultId = defaultDevice.ID;
                }
                catch { /* no default render device available - System Default still falls back to mpv's own "auto" */ }

                foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                {
                    using (device)
                    {
                        string suffix = device.ID == defaultId ? " (Default)" : "";
                        options.Add(new AudioDeviceOption(device.ID, device.FriendlyName + suffix));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsWindow] Audio device enumeration failed: {ex.Message}");
            }

            AudioDeviceComboBox.ItemsSource = options;
            var match = options.FirstOrDefault(o => o.Id == preferredId);
            AudioDeviceComboBox.SelectedItem = match ?? options[0];
        }

        private sealed class DisplayOption
        {
            public System.Windows.Forms.Screen Screen { get; }
            public string Label { get; }

            public DisplayOption(System.Windows.Forms.Screen screen, int number)
            {
                Screen = screen;
                Label = $"Display {number}{(screen.Primary ? " (Primary)" : "")} — {screen.Bounds.Width}x{screen.Bounds.Height}";
            }

            public override string ToString() => Label;
        }

        private sealed class AudioDeviceOption
        {
            public string? Id { get; }
            public string Label { get; }

            public AudioDeviceOption(string? id, string label)
            {
                Id = id;
                Label = label;
            }

            public override string ToString() => Label;
        }

        // --- Show File ---

        private void NewShowButton_Click(object sender, RoutedEventArgs e)
        {
            var confirm = System.Windows.MessageBox.Show(
                "Start a new show? Any unsaved changes will be lost.", "New Show",
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            NewShowRequested?.Invoke();
            Close();
        }

        private void SaveShowButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog { Filter = "Playback Tools Show|*.playback", DefaultExt = ".playback" };
            if (dialog.ShowDialog() != true) return;

            SaveShowRequested?.Invoke(dialog.FileName);
        }

        private void LoadShowButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Filter = "Playback Tools Show|*.playback" };
            if (dialog.ShowDialog() != true) return;

            var confirm = System.Windows.MessageBox.Show(
                "Load this show? Any unsaved changes will be lost.", "Load Show",
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            LoadShowRequested?.Invoke(dialog.FileName);
            Close();
        }

        // --- Device Settings ---

        private async void IdentifyButton_Click(object sender, RoutedEventArgs e)
        {
            IdentifyButton.IsEnabled = false;
            try
            {
                var screens = System.Windows.Forms.Screen.AllScreens;
                var overlays = new List<IdentifyOverlayWindow>();
                for (int i = 0; i < screens.Length; i++)
                {
                    var overlay = new IdentifyOverlayWindow(screens[i], i + 1);
                    overlay.Show();
                    overlays.Add(overlay);
                }

                await Task.Delay(TimeSpan.FromSeconds(3));

                foreach (var overlay in overlays)
                    overlay.Close();
            }
            finally
            {
                IdentifyButton.IsEnabled = true;
            }
        }

        private void RefreshAudioDevicesButton_Click(object sender, RoutedEventArgs e)
        {
            PopulateAudioDeviceComboBox((AudioDeviceComboBox.SelectedItem as AudioDeviceOption)?.Id);
        }

        // --- Playback Settings ---

        private void BackgroundImageButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new BackgroundImageDialog(_viewModel) { Owner = Owner };
            dialog.Show();
            Close();
        }

        // --- Application ---

        private void QuickStartGuideButton_Click(object sender, RoutedEventArgs e)
        {
            var guide = new QuickStartGuideWindow { Owner = this };
            guide.Show();
        }

        private void LicensesButton_Click(object sender, RoutedEventArgs e)
        {
            var licenses = new LicensesWindow { Owner = this };
            licenses.Show();
        }

        private void QuitButton_Click(object sender, RoutedEventArgs e)
        {
            Owner?.Close();
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (DefaultEndActionComboBox.SelectedItem is EndAction selected)
                _viewModel.DefaultEndAction = selected;

            if (DisplayComboBox.SelectedItem is DisplayOption selectedDisplay)
                DisplayChangeRequested?.Invoke(selectedDisplay.Screen);

            if (AudioDeviceComboBox.SelectedItem is AudioDeviceOption selectedAudioDevice)
                AudioDeviceChangeRequested?.Invoke(selectedAudioDevice.Id);

            Close();
        }
    }
}