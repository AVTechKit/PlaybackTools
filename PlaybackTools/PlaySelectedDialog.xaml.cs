using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using PlaybackTools.ViewModels;

namespace PlaybackTools
{
    public partial class PlaySelectedDialog : Window
    {
        private readonly IReadOnlyList<PageViewModel> _pages;

        public ClipSlotViewModel? SelectedClip { get; private set; }

        public PlaySelectedDialog(IReadOnlyList<PageViewModel> pages, ClipSlotViewModel? currentTarget)
        {
            _pages = pages;
            InitializeComponent();

            PageComboBox.ItemsSource = _pages;
            PageComboBox.DisplayMemberPath = nameof(PageViewModel.PageName);

            // Pre-select whatever page/clip is currently assigned, if any.
            var initialPage = currentTarget != null
                ? _pages.FirstOrDefault(p => p.Slots.Contains(currentTarget))
                : null;
            PageComboBox.SelectedItem = initialPage ?? _pages.FirstOrDefault();

            if (currentTarget != null)
                ClipComboBox.SelectedItem = currentTarget;
        }

        private void PageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PageComboBox.SelectedItem is not PageViewModel page) return;

            ClipComboBox.ItemsSource = page.Slots;
            ClipComboBox.DisplayMemberPath = nameof(ClipSlotViewModel.PickerLabel);
            ClipComboBox.SelectedIndex = 0;
        }

        private void ClipComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            OkButton.IsEnabled = ClipComboBox.SelectedItem is ClipSlotViewModel { IsLoaded: true };
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedClip = ClipComboBox.SelectedItem as ClipSlotViewModel;
            DialogResult = true;
        }
    }
}
