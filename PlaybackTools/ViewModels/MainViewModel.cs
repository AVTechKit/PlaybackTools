using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace PlaybackTools.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        public const int PageCount = 10;

        public ObservableCollection<PageViewModel> Pages { get; } = new();

        private PageViewModel _selectedPage;
        public PageViewModel SelectedPage
        {
            get => _selectedPage;
            set => SetProperty(ref _selectedPage, value);
        }

        // Info section
        private string _clipName = "No Clip Loaded";
        public string ClipName { get => _clipName; set => SetProperty(ref _clipName, value); }

        private string _duration = "--:--";
        public string Duration { get => _duration; set => SetProperty(ref _duration, value); }

        private string _endActionDisplay = "--";
        public string EndActionDisplay { get => _endActionDisplay; set => SetProperty(ref _endActionDisplay, value); }

        private string? _selectedDisplayDeviceName;
        public string? SelectedDisplayDeviceName { get => _selectedDisplayDeviceName; set => SetProperty(ref _selectedDisplayDeviceName, value); }

        private string? _selectedAudioDeviceId;
        public string? SelectedAudioDeviceId { get => _selectedAudioDeviceId; set => SetProperty(ref _selectedAudioDeviceId, value); }

        private string _timeRemaining = "--:--";
        public string TimeRemaining { get => _timeRemaining; set => SetProperty(ref _timeRemaining, value); }

        // Navigation section
        private double _volume = 100;
        public double Volume { get => _volume; set => SetProperty(ref _volume, value); }

        private double _fadeTime = 1.0;
        public double FadeTime { get => _fadeTime; set => SetProperty(ref _fadeTime, value); }

        private EndAction _defaultEndAction = EndAction.PauseOnLastFrame;
        public EndAction DefaultEndAction { get => _defaultEndAction; set => SetProperty(ref _defaultEndAction, value); }

        private string? _backgroundImagePath;
        public string? BackgroundImagePath { get => _backgroundImagePath; set => SetProperty(ref _backgroundImagePath, value); }

        private bool _showBackgroundOnStop;
        public bool ShowBackgroundOnStop { get => _showBackgroundOnStop; set => SetProperty(ref _showBackgroundOnStop, value); }

        private bool _showBackgroundOnAudio;
        public bool ShowBackgroundOnAudio { get => _showBackgroundOnAudio; set => SetProperty(ref _showBackgroundOnAudio, value); }

        private ClipSlotViewModel? _activeSlot;
        public ClipSlotViewModel? ActiveSlot { get => _activeSlot; set => SetProperty(ref _activeSlot, value); }

        // Preview section scrub bar
        private double _scrubPosition;
        public double ScrubPosition { get => _scrubPosition; set => SetProperty(ref _scrubPosition, value); }

        // Events the View subscribes to for anything that isn't pure ViewModel
        // state - file dialogs and the actual PlaybackEngine calls are OS/
        // rendering concerns that belong in code-behind, not here.
        public event Action<ClipSlotViewModel>? ClipLoadRequested;
        public event Action<ClipSlotViewModel>? ClipTriggerRequested;
        public event Action? PlayRequested;
        public event Action? PauseRequested;
        public event Action? StopRequested;
        public event Action? MarkInRequested;
        public event Action? MarkOutRequested;

        public ICommand ClipClickCommand { get; }
        public ICommand PlayCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand MarkInCommand { get; }
        public ICommand MarkOutCommand { get; }

        public MainViewModel()
        {
            for (int i = 0; i < PageCount; i++)
                Pages.Add(new PageViewModel(i));

            _selectedPage = Pages[0];

            ClipClickCommand = new RelayCommand(param =>
            {
                if (param is not ClipSlotViewModel slot) return;
                if (slot.IsLoaded)
                    ClipTriggerRequested?.Invoke(slot);
                else
                    ClipLoadRequested?.Invoke(slot);
            });

            PlayCommand = new RelayCommand(_ => PlayRequested?.Invoke());
            PauseCommand = new RelayCommand(_ => PauseRequested?.Invoke());
            StopCommand = new RelayCommand(_ => StopRequested?.Invoke());
            MarkInCommand = new RelayCommand(_ => MarkInRequested?.Invoke());
            MarkOutCommand = new RelayCommand(_ => MarkOutRequested?.Invoke());
        }
    }
}