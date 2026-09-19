using System;
using System.Windows.Input;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace PlaybackTools.ViewModels
{
    public enum EndAction
    {
        LoopCut,
        LoopFade,
        PlayNext,
        PlaySelected,
        PauseOnLastFrame,
        StopToBlack
    }

    public enum MediaType
    {
        None,
        Video,
        Audio,
        Image
    }



    public class ClipSlotViewModel : ViewModelBase
    {
        private string _displayName = "Click to Load";
        private string? _filePath;
        private MediaType _mediaType = MediaType.None;
        private Brush _colorStripBrush = Brushes.Transparent;
        private EndAction _endAction = EndAction.PauseOnLastFrame;

        private string _inPoint = "00:00:00";
        public string InPoint { get => _inPoint; set => SetProperty(ref _inPoint, value); }

        private string _outPoint = "00:00:00";
        public string OutPoint { get => _outPoint; set => SetProperty(ref _outPoint, value); }

        public int SlotIndex { get; internal set; }

        // Raised for actions that need an OS dialog - MainWindow subscribes
        // to these once, for every slot, at startup.
        public event Action<ClipSlotViewModel>? RenameRequested;
        public event Action<ClipSlotViewModel>? ReplaceRequested;
        public event Action<ClipSlotViewModel>? ChooseColorRequested;
        public event Action<ClipSlotViewModel>? PlaySelectedTargetRequested;
        public event Action<ClipSlotViewModel>? MoveSwapRequested;

        public ICommand RenameCommand { get; }
        public ICommand ReplaceCommand { get; }
        public ICommand ChooseColorCommand { get; }
        public ICommand ResetColorCommand { get; }
        public ICommand SetEndActionCommand { get; }
        public ICommand RemoveCommand { get; }
        public ICommand PlaySelectedCommand { get; }
        public ICommand MoveSwapCommand { get; }

        public ClipSlotViewModel? PlaySelectedTarget { get; set; }
        public (float[] Min, float[] Max)? WaveformPeaks { get; set; }

        public ClipSlotViewModel(int slotIndex)
        {
            SlotIndex = slotIndex;

            RenameCommand = new RelayCommand(_ => RenameRequested?.Invoke(this));
            ReplaceCommand = new RelayCommand(_ => ReplaceRequested?.Invoke(this));
            ChooseColorCommand = new RelayCommand(_ => ChooseColorRequested?.Invoke(this));
            PlaySelectedCommand = new RelayCommand(_ => PlaySelectedTargetRequested?.Invoke(this));
            MoveSwapCommand = new RelayCommand(_ => MoveSwapRequested?.Invoke(this));

            ResetColorCommand = new RelayCommand(_ => ColorStripBrush = DefaultColorFor(MediaType));

            SetEndActionCommand = new RelayCommand(param =>
            {
                if (param is EndAction action) EndAction = action;
            });

            RemoveCommand = new RelayCommand(_ =>
            {
                FilePath = null;
                DisplayName = "Click to Load";
                MediaType = MediaType.None;
                EndAction = EndAction.PauseOnLastFrame;
                InPoint = "00:00:00";
                OutPoint = "00:00:00";
                WaveformPeaks = null;
                PlaySelectedTarget = null;
            });
        }

        public string DisplayName
        {
            get => _displayName;
            set
            {
                if (SetProperty(ref _displayName, value))
                    OnPropertyChanged(nameof(PickerLabel));
            }
        }

        public string? FilePath
        {
            get => _filePath;
            set
            {
                if (SetProperty(ref _filePath, value))
                {
                    OnPropertyChanged(nameof(IsLoaded));
                    OnPropertyChanged(nameof(IsEmpty));
                    OnPropertyChanged(nameof(PickerLabel));
                }
            }
        }

        public bool IsLoaded => FilePath != null;

        public bool IsEmpty => !IsLoaded;

        public string PickerLabel => IsLoaded ? DisplayName : "No Clip";

        public MediaType MediaType
        {
            get => _mediaType;
            set
            {
                if (SetProperty(ref _mediaType, value))
                    ColorStripBrush = DefaultColorFor(value);
            }
        }

        public Brush ColorStripBrush
        {
            get => _colorStripBrush;
            set => SetProperty(ref _colorStripBrush, value);
        }

        public EndAction EndAction
        {
            get => _endAction;
            set => SetProperty(ref _endAction, value);
        }

        private static Brush DefaultColorFor(MediaType type) => type switch
        {
            MediaType.Video => Brushes.HotPink,
            MediaType.Image => Brushes.Cyan,
            MediaType.Audio => Brushes.Yellow,
            _ => Brushes.Transparent
        };
    }
}