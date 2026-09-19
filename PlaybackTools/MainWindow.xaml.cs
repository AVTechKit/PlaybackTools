using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Input;
using Microsoft.Win32;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using DragEventArgs = System.Windows.DragEventArgs;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using PlaybackTools.Playback;
using PlaybackTools.ViewModels;
using System.Globalization;
using System.ComponentModel;
using System.Text.Json;
using NAudio.CoreAudioApi;

namespace PlaybackTools
{
    public partial class MainWindow : Window
    {
        private const int CrossfadeHeadStartMs = 150;
        private const int GhostFramePreventionDelayMs = 500; // wait after reaching 0 opacity before actually clearing the player
        private static readonly TimeSpan PositionPollInterval = TimeSpan.FromMilliseconds(200);
        private static readonly TimeSpan PreviewMirrorInterval = TimeSpan.FromMilliseconds(1000.0 / 30.0); // ~30fps

        private static readonly TimeSpan VuMeterInterval = TimeSpan.FromMilliseconds(1000.0 / 30.0); // ~30fps, same cadence as the preview mirror
        private readonly DispatcherTimer _vuTimer = new() { Interval = VuMeterInterval };
        private readonly AudioMeterService _audioMeter = new();
        private double _vuLeftSmoothed, _vuRightSmoothed;

        private readonly PlaybackEngine _engine = new();
        private readonly MainViewModel _viewModel = new();
        private readonly DispatcherTimer _positionTimer = new() { Interval = PositionPollInterval };
        private readonly DispatcherTimer _previewMirrorTimer = new() { Interval = PreviewMirrorInterval };


        private DisplayWindow? _displayWindow;

        private ClipSlotViewModel? _activeSlot;
        private ClipSlotViewModel? _pendingMoveSwapSource;
        private bool _endActionHandledForActiveClip;

        private string? _currentShowName;

        private bool _isScrubbing;
        private double _lastKnownDurationSeconds;

        private double? _activeInPointSeconds;
        private double? _activeOutPointSeconds;
        private double _activeFadeTimeSeconds;

        private const double ShortClipWarningThreshold = 6.0;
        private static readonly SolidColorBrush NormalDurationBrush = MakeFrozenBrush(0xAA, 0xAA, 0xAA);
        private static readonly SolidColorBrush ShortClipWarningBrush = MakeFrozenBrush(0xE8, 0x56, 0x4A);

        private static SolidColorBrush MakeFrozenBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        // Waveform state
        private const int WaveformBucketCount = 2000;
        private CancellationTokenSource? _waveformCts;
        private (float[] min, float[] max)? _currentPeaks;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _viewModel;

            _viewModel.ClipLoadRequested += OnClipLoadRequested;
            _viewModel.ClipTriggerRequested += OnClipTriggerRequested;
            _viewModel.PlayRequested += () => _engine.Resume();
            _viewModel.PauseRequested += () => _engine.Pause();
            _viewModel.StopRequested += OnStopRequested;
            _viewModel.MarkInRequested += () => { if (_viewModel.ActiveSlot != null) _viewModel.ActiveSlot.InPoint = FormatTime(_engine.GetActiveTimePosition()); };
            _viewModel.MarkOutRequested += () => { if (_viewModel.ActiveSlot != null) _viewModel.ActiveSlot.OutPoint = FormatTime(_engine.GetActiveTimePosition()); };
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.Volume))
                    _engine.SetVolume(_viewModel.Volume);
                else if (e.PropertyName == nameof(MainViewModel.ScrubPosition))
                    UpdateWaveformCursor();
                else if (e.PropertyName is nameof(MainViewModel.ShowBackgroundOnStop)
                                        or nameof(MainViewModel.ShowBackgroundOnAudio)
                                        or nameof(MainViewModel.BackgroundImagePath))
                    RefreshBackgroundMask();
            };

            foreach (var page in _viewModel.Pages)
            {
                page.RenameRequested += OnPageRenameRequested;
                page.ChooseColorRequested += OnPageColorPickRequested;
                foreach (var slot in page.Slots)
                {
                    slot.RenameRequested += OnClipRenameRequested;
                    slot.ReplaceRequested += ShowLoadDialog;
                    slot.ChooseColorRequested += OnClipColorPickRequested;
                    slot.PlaySelectedTargetRequested += OnPlaySelectedTargetRequested;
                    slot.MoveSwapRequested += OnMoveSwapRequested;
                }
            }

            SetUpDisplayWindow();

            _positionTimer.Tick += PositionTimer_Tick;
            _positionTimer.Start();

            _previewMirrorTimer.Tick += PreviewMirrorTimer_Tick;
            _previewMirrorTimer.Start();
            _audioMeter.SetDevice(_viewModel.SelectedAudioDeviceId);
            _vuTimer.Tick += VuTimer_Tick;
            _vuTimer.Start();
        }

        private void SetUpDisplayWindow()
        {
            var screens = System.Windows.Forms.Screen.AllScreens;
            if (screens.Length < 2)
            {
                System.Windows.MessageBox.Show("No Second Display Found.", "Playback Tools",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return; // Show Building Mode - Control Window only, no live output
            }

            var targetScreen = screens.FirstOrDefault(s => !s.Primary) ?? screens[1];
            _displayWindow = new DisplayWindow(_engine, _viewModel, targetScreen);
            _displayWindow.Show();
            _viewModel.SelectedDisplayDeviceName = targetScreen.DeviceName;
        }

        private void VuTimer_Tick(object? sender, EventArgs e)
        {
            var (rawLeft, rawRight) = _audioMeter.ReadPeaks();

            const double attack = 0.6;   // fast rise - meter jumps to a hit quickly
            const double release = 0.15; // slower fall - the graceful decay real VU meters are known for

            _vuLeftSmoothed += (rawLeft - _vuLeftSmoothed) * (rawLeft > _vuLeftSmoothed ? attack : release);
            _vuRightSmoothed += (rawRight - _vuRightSmoothed) * (rawRight > _vuRightSmoothed ? attack : release);

            VuLeftMaskScale.ScaleY = 1.0 - Clamp01(_vuLeftSmoothed);
            VuRightMaskScale.ScaleY = 1.0 - Clamp01(_vuRightSmoothed);
        }

        private static double Clamp01(double value) => value < 0 ? 0 : value > 1 ? 1 : value;

        private void OnDisplayChangeRequested(System.Windows.Forms.Screen targetScreen)
        {
            if (_displayWindow == null)
            {
                // Was in Show Building Mode (no second display at startup) - build it now.
                _displayWindow = new DisplayWindow(_engine, _viewModel, targetScreen);
                _displayWindow.Show();
            }
            else
            {
                _displayWindow.MoveToScreen(targetScreen);
            }

            _viewModel.SelectedDisplayDeviceName = targetScreen.DeviceName;
        }

        private void OnAudioDeviceChangeRequested(string? deviceId)
        {
            _engine.SetAudioDevice(deviceId);
            _viewModel.SelectedAudioDeviceId = deviceId;
            _audioMeter.SetDevice(deviceId);
        }

        private void UpdateTitleBar()
        {
            Title = _currentShowName != null
                ? $"Playback Tools - Control Window - {_currentShowName}"
                : "Playback Tools - Control Window";
        }

        private void StopAndResetForShowChange()
        {
            _engine.StopToBlack();
            ResetInfoPanel();
            RefreshBackgroundMask();
        }

        private void OnNewShowRequested()
        {
            StopAndResetForShowChange();

            foreach (var page in _viewModel.Pages)
            {
                page.RestoreDefaultNameCommand.Execute(null);
                page.ResetColorCommand.Execute(null);
                foreach (var slot in page.Slots)
                    slot.RemoveCommand.Execute(null);
            }

            _viewModel.DefaultEndAction = EndAction.PauseOnLastFrame;
            _viewModel.Volume = 100;
            _viewModel.FadeTime = 1.0;
            _viewModel.BackgroundImagePath = null;
            _viewModel.ShowBackgroundOnStop = false;
            _viewModel.ShowBackgroundOnAudio = false;
            _viewModel.SelectedPage = _viewModel.Pages[0];

            _currentShowName = null;
            UpdateTitleBar();
        }

        private void OnSaveShowRequested(string filePath)
        {
            string showName = Path.GetFileNameWithoutExtension(filePath);
            var showFile = ShowFileConverter.ToShowFile(_viewModel, showName);
            string json = JsonSerializer.Serialize(showFile, new JsonSerializerOptions { WriteIndented = true });

            try
            {
                File.WriteAllText(filePath, json);
                _currentShowName = showName;
                UpdateTitleBar();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Could not save show: {ex.Message}", "Playback Tools",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void OnLoadShowRequested(string filePath)
        {
            ShowFile showFile;
            try
            {
                string json = File.ReadAllText(filePath);
                showFile = JsonSerializer.Deserialize<ShowFile>(json) ?? throw new InvalidDataException("Empty or invalid show file.");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Could not load show: {ex.Message}", "Playback Tools",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return; // current show is untouched - nothing was stopped/reset yet
            }

            StopAndResetForShowChange();

            var missingFiles = ShowFileConverter.ApplyToViewModel(showFile, _viewModel);
            _viewModel.SelectedPage = _viewModel.Pages[0];

            string? audioDeviceId = _viewModel.SelectedAudioDeviceId;
            if (audioDeviceId != null)
            {
                bool stillExists;
                try
                {
                    using var enumerator = new MMDeviceEnumerator();
                    stillExists = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                        .Any(d => { using (d) return d.ID == audioDeviceId; });
                }
                catch { stillExists = false; }

                if (!stillExists)
                {
                    audioDeviceId = null;
                    _viewModel.SelectedAudioDeviceId = null;
                }
            }
            _engine.SetAudioDevice(audioDeviceId);

            var matchingScreen = System.Windows.Forms.Screen.AllScreens
                .FirstOrDefault(s => s.DeviceName == _viewModel.SelectedDisplayDeviceName);
            if (matchingScreen != null)
                OnDisplayChangeRequested(matchingScreen);

            foreach (var page in _viewModel.Pages)
                foreach (var slot in page.Slots)
                    if (slot.FilePath != null && slot.WaveformPeaks == null)
                        PrecomputeWaveformAsync(slot);

            _currentShowName = showFile.ShowName;
            UpdateTitleBar();

            if (missingFiles.Count > 0)
            {
                var dialog = new MissingFilesDialog(missingFiles) { Owner = this };
                dialog.Show();
            }
        }

        private void PreviewMirrorTimer_Tick(object? sender, EventArgs e)
        {
            if (_displayWindow == null) return;

            int fullW = (int)_displayWindow.ActualWidth;
            int fullH = (int)_displayWindow.ActualHeight;
            if (fullW <= 0 || fullH <= 0) return;

            const int targetW = 640;
            int targetH = (int)(targetW * ((double)fullH / fullW));

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                var brush = new VisualBrush(_displayWindow) { Stretch = Stretch.Uniform };
                dc.DrawRectangle(brush, null, new Rect(0, 0, targetW, targetH));
            }

            var bitmap = new RenderTargetBitmap(targetW, targetH, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            PreviewImage.Source = bitmap;
        }

        private void OnClipLoadRequested(ClipSlotViewModel slot)
        {
            if (TryCompletePendingMoveSwap(slot)) return;
            ShowLoadDialog(slot);
        }

        private void ShowLoadDialog(ClipSlotViewModel slot)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Media files|*.mp4;*.mov;*.avi;*.mkv;*.wav;*.mp3;*.png;*.jpg;*.jpeg|All files|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() != true) return;

            LoadFilesStartingAt(slot, dialog.FileNames);
        }

        private void LoadFilesStartingAt(ClipSlotViewModel startSlot, IReadOnlyList<string> filePaths)
        {
            if (filePaths.Count == 0) return;

            var allSlots = _viewModel.Pages.SelectMany(p => p.Slots).ToList();
            int cursor = allSlots.IndexOf(startSlot);
            if (cursor < 0) return; // defensive - shouldn't happen

            int placed = 0;
            for (int i = 0; i < filePaths.Count; i++)
            {
                ClipSlotViewModel? destination;

                if (i == 0)
                {
                    destination = startSlot;
                }
                else
                {
                    destination = allSlots.Skip(cursor + 1).FirstOrDefault(s => s.IsEmpty);
                    if (destination == null) break; // ran out of empty slots
                    cursor = allSlots.IndexOf(destination);
                }

                LoadFileIntoSlot(destination, filePaths[i]);
                placed++;
            }

            if (placed < filePaths.Count)
            {
                System.Windows.MessageBox.Show(
                    $"Loaded {placed} of {filePaths.Count} files - ran out of empty clip slots.",
                    "Playback Tools", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }

        private void LoadFileIntoSlot(ClipSlotViewModel slot, string filePath)
        {
            slot.FilePath = filePath;
            slot.DisplayName = Path.GetFileNameWithoutExtension(filePath);
            slot.MediaType = GuessMediaType(filePath);
            slot.EndAction = _viewModel.DefaultEndAction;
            slot.InPoint = "00:00:00";
            slot.OutPoint = "00:00:00";
            slot.WaveformPeaks = null;
            PrecomputeWaveformAsync(slot);
        }

        private async void PrecomputeWaveformAsync(ClipSlotViewModel slot)
        {
            if (slot.MediaType == MediaType.Image || slot.FilePath == null) return;
            string originalPath = slot.FilePath;

            try
            {
                var peaks = await Task.Run(() => WaveformGenerator.GeneratePeaks(originalPath, WaveformBucketCount));
                if (slot.FilePath != originalPath) return; // replaced/removed again while we were generating - discard
                slot.WaveformPeaks = peaks;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Waveform] precompute failed for {slot.DisplayName}: {ex.Message}");
            }
        }

        private void ClipButton_DragEnter(object sender, DragEventArgs e)
        {
            bool hasSupportedFile = e.Data.GetDataPresent(DataFormats.FileDrop)
                && ((string[])e.Data.GetData(DataFormats.FileDrop)).Any(p => AllowedMediaExtensions.Contains(Path.GetExtension(p)));

            e.Effects = hasSupportedFile ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void ClipButton_Drop(object sender, DragEventArgs e)
        {
            CancelPendingMoveSwap();
            if (sender is not System.Windows.Controls.Button button || button.DataContext is not ClipSlotViewModel slot) return;
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            var allPaths = (string[])e.Data.GetData(DataFormats.FileDrop);
            var paths = allPaths.Where(p => File.Exists(p) && AllowedMediaExtensions.Contains(Path.GetExtension(p))).ToArray();

            if (paths.Length == 0)
            {
                System.Windows.MessageBox.Show(
                    "No supported media files were in that drop. Supported types: mp4, mov, avi, mkv, wav, mp3, png, jpg, jpeg.",
                    "Playback Tools", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                e.Handled = true;
                return;
            }

            LoadFilesStartingAt(slot, paths);
            e.Handled = true;
        }

        private void OnClipRenameRequested(ClipSlotViewModel slot)
        {
            var dialog = new RenameDialog(slot.DisplayName) { Owner = this };
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ResultText))
                slot.DisplayName = dialog.ResultText;
        }

        private void OnClipColorPickRequested(ClipSlotViewModel slot)
        {
            using var dialog = new System.Windows.Forms.ColorDialog();
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            var c = dialog.Color;
            slot.ColorStripBrush = new SolidColorBrush(Color.FromArgb(c.A, c.R, c.G, c.B));
        }

        private void OnPageRenameRequested(PageViewModel page)
        {
            var dialog = new RenameDialog(page.PageName) { Owner = this };
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ResultText))
                page.PageName = dialog.ResultText;
        }

        private void OnPageColorPickRequested(PageViewModel page)
        {
            using var dialog = new System.Windows.Forms.ColorDialog();
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            var c = dialog.Color;
            page.BorderBrush = new SolidColorBrush(Color.FromArgb(c.A, c.R, c.G, c.B));
        }

        private void OnPlaySelectedTargetRequested(ClipSlotViewModel slot)
        {
            var dialog = new PlaySelectedDialog(_viewModel.Pages, slot.PlaySelectedTarget) { Owner = this };
            if (dialog.ShowDialog() == true && dialog.SelectedClip != null)
            {
                slot.PlaySelectedTarget = dialog.SelectedClip;
                slot.EndAction = EndAction.PlaySelected;
            }
            // Canceled, or no valid clip chosen - leave the End Action as it was.
        }

        private void OnMoveSwapRequested(ClipSlotViewModel slot)
        {
            _pendingMoveSwapSource = slot;
            Cursor = System.Windows.Input.Cursors.Hand;
        }

        private bool TryCompletePendingMoveSwap(ClipSlotViewModel destination)
        {
            if (_pendingMoveSwapSource == null) return false;

            var source = _pendingMoveSwapSource;
            CancelPendingMoveSwap();

            if (destination == source) return true; // clicking the source again cancels

            SwapClips(source, destination);
            _viewModel.SelectedPage = _viewModel.Pages[source.SlotIndex / PageViewModel.SlotsPerPage];
            return true;
        }

        private void CancelPendingMoveSwap()
        {
            _pendingMoveSwapSource = null;
            Cursor = System.Windows.Input.Cursors.Arrow;
        }

        private void SwapClips(ClipSlotViewModel a, ClipSlotViewModel b)
        {
            if (a == b) return;

            int pageIndexA = a.SlotIndex / PageViewModel.SlotsPerPage;
            int localIndexA = a.SlotIndex % PageViewModel.SlotsPerPage;
            int pageIndexB = b.SlotIndex / PageViewModel.SlotsPerPage;
            int localIndexB = b.SlotIndex % PageViewModel.SlotsPerPage;

            _viewModel.Pages[pageIndexA].Slots[localIndexA] = b;
            _viewModel.Pages[pageIndexB].Slots[localIndexB] = a;

            int originalIndexA = a.SlotIndex;
            a.SlotIndex = b.SlotIndex;
            b.SlotIndex = originalIndexA;
        }

        private static readonly HashSet<string> AllowedMediaExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".avi", ".mov", ".mkv", ".wav", ".mp3", ".png", ".jpg", ".jpeg"
        };

        internal static MediaType GuessMediaType(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".mp3" or ".wav" => MediaType.Audio,
                ".png" or ".jpg" or ".jpeg" => MediaType.Image,
                _ => MediaType.Video
            };
        }

        private void OnClipTriggerRequested(ClipSlotViewModel slot)
        {
            if (TryCompletePendingMoveSwap(slot)) return;
            TriggerSlot(slot);
        }

        private async void TriggerSlot(ClipSlotViewModel slot)
        {
            if (slot.FilePath == null || _displayWindow == null) return;

            // Compare BEFORE we touch _activeSlot - it still holds the outgoing clip here.
            bool wasShowingBackground = _activeSlot == null
                ? _viewModel.ShowBackgroundOnStop
                : (_activeSlot.MediaType == MediaType.Audio && _viewModel.ShowBackgroundOnAudio);
            bool willShowBackground = slot.MediaType == MediaType.Audio && _viewModel.ShowBackgroundOnAudio;

            if (wasShowingBackground && !willShowBackground)
                _displayWindow.FadeBackgroundOut(_viewModel.FadeTime * 1000);

            if (_activeSlot != null)
                _activeSlot.PropertyChanged -= ActiveSlot_PropertyChanged;

            _activeSlot = slot;
            _viewModel.ActiveSlot = slot;
            _activeSlot.PropertyChanged += ActiveSlot_PropertyChanged;
            _endActionHandledForActiveClip = false;

            double? inSeconds = ParseTimeToSeconds(slot.InPoint);
            double? outSeconds = ParseTimeToSeconds(slot.OutPoint);

            _activeInPointSeconds = inSeconds;
            _activeOutPointSeconds = outSeconds;
            _activeFadeTimeSeconds = _viewModel.FadeTime;

            bool loopOn = slot.EndAction == EndAction.LoopCut;
            var (incomingIndex, outgoingIndex) = _engine.TriggerClip(slot.FilePath, loopOn, inSeconds, outSeconds);
            int expectedGeneration = _engine.GenerationFor(outgoingIndex);

            _viewModel.ClipName = $"Clip Name: {slot.DisplayName}";
            _viewModel.EndActionDisplay = FormatEndAction(slot.EndAction);

            LoadWaveformForSlot(slot);

            await Task.Delay(CrossfadeHeadStartMs);

            var incomingView = incomingIndex == 0 ? _displayWindow.ViewA : _displayWindow.ViewB;
            var outgoingView = outgoingIndex == 0 ? _displayWindow.ViewA : _displayWindow.ViewB;
            double fadeMs = _activeFadeTimeSeconds * 1000;

            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(fadeMs));
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(fadeMs));

            // Manual audio crossfade: mpv's per-instance volume can't be animated
            // directly, so we sample the video opacity - which IS already animating -
            // every frame and push that same fraction into each player's volume.
            var audioFadeStopwatch = System.Diagnostics.Stopwatch.StartNew();
            void AudioFadeTick(object? s, EventArgs e)
            {
                _engine.SetPlayerVolume(incomingIndex, incomingView.Opacity * _viewModel.Volume);
                _engine.SetPlayerVolume(outgoingIndex, outgoingView.Opacity * _viewModel.Volume);

                if (audioFadeStopwatch.ElapsedMilliseconds >= fadeMs)
                    CompositionTarget.Rendering -= AudioFadeTick;
            }
            CompositionTarget.Rendering += AudioFadeTick;

            fadeOut.Completed += async (s, e) =>
            {
                RefreshBackgroundMask();
                await Task.Delay(GhostFramePreventionDelayMs);
                _engine.StopPlayer(outgoingIndex, expectedGeneration);
                _engine.SetPlayerVolume(outgoingIndex, _viewModel.Volume); // restore for next use
                
            };

            incomingView.BeginAnimation(OpacityProperty, fadeIn);
            outgoingView.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void ActiveSlot_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_activeSlot == null || _activeSlot.EndAction != EndAction.LoopCut) return;
            if (e.PropertyName != nameof(ClipSlotViewModel.InPoint) && e.PropertyName != nameof(ClipSlotViewModel.OutPoint)) return;

            double? inSeconds = ParseTimeToSeconds(_activeSlot.InPoint);
            double? outSeconds = ParseTimeToSeconds(_activeSlot.OutPoint);
            _engine.UpdateActiveAbLoop(inSeconds, outSeconds);
        }

        private void PositionTimer_Tick(object? sender, EventArgs e)
        {
            if (_activeSlot == null) return;

            string? posStr = _engine.GetActiveTimePosition();
            string? durStr = _engine.GetActiveDuration();

            if (double.TryParse(durStr, out double duration) && duration > 0)
            {
                _viewModel.Duration = FormatTime(durStr);
                _lastKnownDurationSeconds = duration;
            }

            if (!double.TryParse(posStr, out double pos) || duration <= 0)
                return;

            bool hasValidOutPoint = _activeOutPointSeconds.HasValue && _activeOutPointSeconds.Value > 0 && _activeOutPointSeconds.Value <= duration
                && (!_activeInPointSeconds.HasValue || _activeOutPointSeconds.Value > _activeInPointSeconds.Value);
            double effectiveEnd = hasValidOutPoint ? _activeOutPointSeconds!.Value : duration;
            double effectiveStart = (_activeInPointSeconds.HasValue && _activeInPointSeconds.Value > 0) ? _activeInPointSeconds.Value : 0;

            double trimmedDuration = effectiveEnd - effectiveStart;
            bool isShortClip = trimmedDuration < ShortClipWarningThreshold;
            DurationTextBlock.Foreground = isShortClip ? ShortClipWarningBrush : NormalDurationBrush;
            DurationTextBlock.ToolTip = isShortClip
                ? "Clips trimmed to less than 6 seconds may experience lag during crossfade."
                : null;

            double remaining = Math.Max(0, effectiveEnd - pos);
            _viewModel.TimeRemaining = FormatTime(remaining.ToString());

            if (!_isScrubbing)
                _viewModel.ScrubPosition = (pos / duration) * 100.0;

            if (_endActionHandledForActiveClip) return;

            bool isFadeGroupAction = _activeSlot.EndAction is EndAction.LoopFade or EndAction.PlayNext
                or EndAction.PlaySelected or EndAction.StopToBlack;

            if (isFadeGroupAction)
            {
                if (remaining <= _activeFadeTimeSeconds)
                {
                    _endActionHandledForActiveClip = true;
                    HandleFadeGroupEndAction(_activeSlot);
                }
                return;
            }

            if (_activeSlot.EndAction == EndAction.PauseOnLastFrame && hasValidOutPoint)
            {
                if (remaining <= 0)
                {
                    _engine.Pause();
                    _endActionHandledForActiveClip = true;
                }
                return;
            }

            if (_engine.GetActiveEofReached() == "yes")
                _endActionHandledForActiveClip = true;
        }

        private void ScrubSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => BeginScrub();
        private void ScrubSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => EndScrub();

        private void ScrubSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isScrubbing)
                UpdateScrubTooltip();
        }

        private void WaveformContainer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_activeSlot == null) return;
            Mouse.Capture(WaveformContainer);
            BeginScrub();
            UpdateScrubPositionFromMouseX(e.GetPosition(WaveformContainer).X);
        }

        private void WaveformContainer_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isScrubbing) return;
            UpdateScrubPositionFromMouseX(e.GetPosition(WaveformContainer).X);
        }

        private void WaveformContainer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isScrubbing) return;
            Mouse.Capture(null);
            EndScrub();
        }

        private void UpdateScrubPositionFromMouseX(double x)
        {
            double width = WaveformContainer.ActualWidth;
            if (width <= 0) return;

            double pct = Math.Clamp(x / width, 0, 1) * 100.0;
            _viewModel.ScrubPosition = pct; // also drives the slider, via its two-way binding
            UpdateScrubTooltip();
        }

        /// <summary>Shared drag-start for both the scrub slider and the waveform -
        /// they represent the same position and should behave identically.</summary>
        private void BeginScrub()
        {
            if (_activeSlot == null) return;
            _isScrubbing = true;
            ScrubTooltipPopup.IsOpen = true;
            UpdateScrubTooltip();
        }

        /// <summary>Shared drag-end - only actually executes the seek on release,
        /// per spec; dragging itself just previews the position via the tooltip.</summary>
        private void EndScrub()
        {
            if (!_isScrubbing) return;
            _isScrubbing = false;
            ScrubTooltipPopup.IsOpen = false;

            if (_lastKnownDurationSeconds > 0)
            {
                double targetSeconds = (_viewModel.ScrubPosition / 100.0) * _lastKnownDurationSeconds;
                _engine.SeekActive(targetSeconds);
            }
        }

        private void UpdateScrubTooltip()
        {
            double posSeconds = (_viewModel.ScrubPosition / 100.0) * _lastKnownDurationSeconds;
            ScrubTooltipText.Text = $"{FormatTime(posSeconds.ToString())}/{FormatTime(_lastKnownDurationSeconds.ToString())}";
        }

        // --- Navigation and Skip button ---

        private void SkipToStartButton_Click(object sender, RoutedEventArgs e)
        {
            if (_activeSlot == null) return;
            _engine.SeekActive(0);
        }

        private void SkipToEndButton_Click(object sender, RoutedEventArgs e) => SkipToTail(5.5);

        private void SkipBackwardButton_Click(object sender, RoutedEventArgs e) => SkipRelative(-20);
        private void SkipForwardButton_Click(object sender, RoutedEventArgs e) => SkipRelative(20);

        private void Last15Button_Click(object sender, RoutedEventArgs e) => SkipToTail(15);
        private void Last30Button_Click(object sender, RoutedEventArgs e) => SkipToTail(30);
        private void ResetInButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.ActiveSlot != null) _viewModel.ActiveSlot.InPoint = "00:00:00";
        }

        private void ResetOutButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.ActiveSlot != null) _viewModel.ActiveSlot.OutPoint = "00:00:00";
        }

        private void SkipRelative(double deltaSeconds)
        {
            if (_activeSlot == null || _lastKnownDurationSeconds <= 0) return;
            if (!double.TryParse(_engine.GetActiveTimePosition(), out double currentPos)) return;

            double target = Math.Clamp(currentPos + deltaSeconds, 0, _lastKnownDurationSeconds);
            _engine.SeekActive(target);
        }

        private void SkipToTail(double secondsFromEnd)
        {
            if (_activeSlot == null || _lastKnownDurationSeconds <= 0) return;

            double target = Math.Max(0, _lastKnownDurationSeconds - secondsFromEnd);
            _engine.SeekActive(target);
        }

        // --- WaveForm

        private void UpdateWaveformCursor()
        {
            if (_activeSlot == null)
            {
                WaveformCursor.Visibility = Visibility.Collapsed;
                return;
            }

            double width = WaveformContainer.ActualWidth;
            double x = (_viewModel.ScrubPosition / 100.0) * width;
            WaveformCursor.X1 = x;
            WaveformCursor.X2 = x;
            WaveformCursor.Y1 = 0;
            WaveformCursor.Y2 = WaveformContainer.ActualHeight;
            WaveformCursor.Visibility = Visibility.Visible;
        }

        private void WaveformContainer_SizeChanged(object sender, SizeChangedEventArgs e) => RenderWaveformPolygon();

        private void LoadWaveformForSlot(ClipSlotViewModel slot)
        {
            _waveformCts?.Cancel();
            WaveformPolygon.Points.Clear();
            _currentPeaks = null;

            if (slot.WaveformPeaks is { } cachedPeaks)
            {
                // Already generated at load time - draw immediately, no ffmpeg call needed.
                _currentPeaks = cachedPeaks;
                RenderWaveformPolygon();
                return;
            }

            // Not cached yet (triggered before background precompute finished) - generate
            // fresh, same as before, and cache the result this time for next trigger.
            if (slot.FilePath != null)
                LoadWaveformAsync(slot.FilePath, slot);
        }

        private async void LoadWaveformAsync(string filePath, ClipSlotViewModel slot)
        {
            var cts = new CancellationTokenSource();
            _waveformCts = cts;

            try
            {
                var peaks = await Task.Run(() => WaveformGenerator.GeneratePeaks(filePath, WaveformBucketCount));
                if (cts.IsCancellationRequested) return; // superseded by a newer clip trigger

                if (slot.FilePath == filePath) slot.WaveformPeaks = peaks; // cache for next time
                _currentPeaks = peaks;
                RenderWaveformPolygon();
            }
            catch (Exception ex)
            {
                // Non-fatal - e.g. ffmpeg isn't installed/on PATH on this machine.
                // The waveform just stays blank for this clip.
                System.Diagnostics.Debug.WriteLine($"[Waveform] failed to load: {ex.Message}");
            }
        }

        private void RenderWaveformPolygon()
        {
            if (_currentPeaks == null) return;
            var (min, max) = _currentPeaks.Value;

            double width = WaveformContainer.ActualWidth;
            double height = WaveformContainer.ActualHeight;
            if (width <= 0 || height <= 0) return;

            double midY = height / 2;
            int count = max.Length;
            var points = new PointCollection(count * 2);

            for (int i = 0; i < count; i++)
            {
                double x = (double)i / (count - 1) * width;
                points.Add(new Point(x, midY - max[i] * midY));
            }
            for (int i = count - 1; i >= 0; i--)
            {
                double x = (double)i / (count - 1) * width;
                points.Add(new Point(x, midY - min[i] * midY));
            }

            WaveformPolygon.Points = points;
        }

        private void HandleFadeGroupEndAction(ClipSlotViewModel slot)
        {
            switch (slot.EndAction)
            {
                case EndAction.LoopFade:
                    TriggerSlot(slot); // same clip, fresh instance on the other player - a self-crossfade
                    break;
                case EndAction.PlayNext:
                    var next = FindNextLoadedSlot(slot);
                    if (next != null) TriggerSlot(next);
                    break;
                case EndAction.StopToBlack:
                    FadeActiveToBlack();
                    break;
                case EndAction.PlaySelected:
                    if (slot.PlaySelectedTarget != null && slot.PlaySelectedTarget.IsLoaded)
                        TriggerSlot(slot.PlaySelectedTarget);
                    break;
            }
        }

        /// <summary>Fades the currently active player's opacity and volume down to
        /// zero over Fade Time - the fade-based Stop to Black end action. Unlike
        /// TriggerClip, there's no incoming clip here, just a fade to nothing.</summary>
        private async void FadeActiveToBlack()
        {
            if (_displayWindow == null) return;

            int activeIndex = _engine.ActiveIndex;
            int expectedGeneration = _engine.BeginStopToBlack();
            var activeView = activeIndex == 0 ? _displayWindow.ViewA : _displayWindow.ViewB;
            double fadeMs = _activeFadeTimeSeconds * 1000;

            var fadeOut = new DoubleAnimation(activeView.Opacity, 0, TimeSpan.FromMilliseconds(fadeMs));

            var audioFadeStopwatch = System.Diagnostics.Stopwatch.StartNew();
            void AudioFadeTick(object? s, EventArgs e)
            {
                _engine.SetPlayerVolume(activeIndex, activeView.Opacity * _viewModel.Volume);
                if (audioFadeStopwatch.ElapsedMilliseconds >= fadeMs)
                    CompositionTarget.Rendering -= AudioFadeTick;
            }
            CompositionTarget.Rendering += AudioFadeTick;

            fadeOut.Completed += async (s, e) =>
            {
                RefreshBackgroundMask();
                await Task.Delay(GhostFramePreventionDelayMs);
                _engine.StopPlayer(activeIndex, expectedGeneration);
                _engine.SetPlayerVolume(activeIndex, _viewModel.Volume); // restore for next use
                
            };

            activeView.BeginAnimation(OpacityProperty, fadeOut);
            if (_viewModel.ShowBackgroundOnStop)
                _displayWindow.FadeBackgroundIn(fadeMs);

            ResetInfoPanel();

            ResetInfoPanel();
        }

        private ClipSlotViewModel? FindNextLoadedSlot(ClipSlotViewModel current)
        {
            var all = _viewModel.Pages.SelectMany(p => p.Slots).OrderBy(s => s.SlotIndex).ToList();
            int idx = all.FindIndex(s => s.SlotIndex == current.SlotIndex);
            for (int i = idx + 1; i < all.Count; i++)
                if (all[i].IsLoaded) return all[i];
            return null;
        }

        private static string FormatEndAction(EndAction action) => action switch
        {
            EndAction.LoopCut => "Loop (Cut)",
            EndAction.LoopFade => "Loop (Fade)",
            EndAction.PlayNext => "Play Next",
            EndAction.PlaySelected => "Play Selected",
            EndAction.PauseOnLastFrame => "Pause on Last Frame",
            EndAction.StopToBlack => "Stop to Black",
            _ => "--"
        };

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settings = new SettingsWindow(_viewModel) { Owner = this };
            settings.DisplayChangeRequested += OnDisplayChangeRequested;
            settings.AudioDeviceChangeRequested += OnAudioDeviceChangeRequested;
            settings.NewShowRequested += OnNewShowRequested;
            settings.SaveShowRequested += OnSaveShowRequested;
            settings.LoadShowRequested += OnLoadShowRequested;
            settings.Show();
        }

        private void OnStopRequested()
        {
            _engine.StopToBlack();
            ResetInfoPanel();
            RefreshBackgroundMask();
        }

        private void ResetInfoPanel()
        {
            if (_activeSlot != null)
                _activeSlot.PropertyChanged -= ActiveSlot_PropertyChanged;
            _activeSlot = null;
            _viewModel.ActiveSlot = null;
            _lastKnownDurationSeconds = 0;
            _waveformCts?.Cancel();
            _currentPeaks = null;
            WaveformPolygon.Points.Clear();
            WaveformCursor.Visibility = Visibility.Collapsed;
            _viewModel.ClipName = "No Clip Loaded";
            _viewModel.Duration = "--:--";
            _viewModel.EndActionDisplay = "--";
            _viewModel.TimeRemaining = "--:--";
            _viewModel.ScrubPosition = 0;
            _activeInPointSeconds = null;
            _activeOutPointSeconds = null;
            _activeFadeTimeSeconds = 0;
            DurationTextBlock.Foreground = NormalDurationBrush;
            DurationTextBlock.ToolTip = null;
        }

        private void RefreshBackgroundMask()
        {
            if (_displayWindow == null) return;

            bool wantBackground = _activeSlot == null
                ? _viewModel.ShowBackgroundOnStop
                : (_activeSlot.MediaType == MediaType.Audio && _viewModel.ShowBackgroundOnAudio);

            _displayWindow.SetBackgroundVisible(wantBackground);
        }

        private static string FormatTime(string? mpvSeconds)
        {
            if (!double.TryParse(mpvSeconds, out double seconds)) return "00:00:00";
            var ts = TimeSpan.FromSeconds(seconds);
            return ts.ToString(@"hh\:mm\:ss");
        }

        private static double? ParseTimeToSeconds(string? text)
        {
            if (TimeSpan.TryParseExact(text, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out var ts) && ts >= TimeSpan.Zero)
                return ts.TotalSeconds;
            return null;
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape && _pendingMoveSwapSource != null)
                CancelPendingMoveSwap();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            var confirm = System.Windows.MessageBox.Show(
                "Close Playback Tools? Any unsaved changes will be lost.", "Playback Tools",
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            _positionTimer.Stop();
            _previewMirrorTimer.Stop();
            _vuTimer.Stop();
            _audioMeter.Dispose();
            _displayWindow?.AllowClose();
            _displayWindow?.Close();
            _displayWindow?.Close();
            _engine.Dispose();
            base.OnClosed(e);
        }
    }
}