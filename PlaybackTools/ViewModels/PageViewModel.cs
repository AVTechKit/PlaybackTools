using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace PlaybackTools.ViewModels
{
    public class PageViewModel : ViewModelBase
    {
        public const int SlotsPerPage = 30;

        private static readonly Brush DefaultBorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));

        private string _pageName;
        private Brush _borderBrush = DefaultBorderBrush;

        public int PageIndex { get; }

        public string DefaultPageName => $"Page {PageIndex + 1}";

        public string PageName
        {
            get => _pageName;
            set => SetProperty(ref _pageName, value);
        }

        public Brush BorderBrush
        {
            get => _borderBrush;
            set => SetProperty(ref _borderBrush, value);
        }

        public ObservableCollection<ClipSlotViewModel> Slots { get; } = new();

        public event Action<PageViewModel>? RenameRequested;
        public event Action<PageViewModel>? ChooseColorRequested;

        public ICommand RenameCommand { get; }
        public ICommand RestoreDefaultNameCommand { get; }
        public ICommand ChooseColorCommand { get; }
        public ICommand ResetColorCommand { get; }

        public PageViewModel(int pageIndex)
        {
            PageIndex = pageIndex;
            _pageName = DefaultPageName;

            for (int i = 0; i < SlotsPerPage; i++)
                Slots.Add(new ClipSlotViewModel(pageIndex * SlotsPerPage + i));

            RenameCommand = new RelayCommand(_ => RenameRequested?.Invoke(this));
            RestoreDefaultNameCommand = new RelayCommand(_ => PageName = DefaultPageName);
            ChooseColorCommand = new RelayCommand(_ => ChooseColorRequested?.Invoke(this));
            ResetColorCommand = new RelayCommand(_ => BorderBrush = DefaultBorderBrush);
        }
    }
}