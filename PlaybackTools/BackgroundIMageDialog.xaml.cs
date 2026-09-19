using System;
using System.Windows;
using System.Windows.Media.Imaging;
using PlaybackTools.ViewModels;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace PlaybackTools
{
    /// <summary>Every control here is immediate-effect - Load/Remove apply the moment
    /// you click them, and the checkboxes are two-way bound straight to the ViewModel -
    /// so unlike the main Settings window, there's nothing to stage, just a Close button.</summary>
    public partial class BackgroundImageDialog : Window
    {
        private readonly MainViewModel _viewModel;

        public BackgroundImageDialog(MainViewModel viewModel)
        {
            _viewModel = viewModel;
            InitializeComponent();
            DataContext = _viewModel;

            RefreshPreview();
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image files|*.png;*.jpg;*.jpeg"
            };

            if (dialog.ShowDialog() != true) return;

            _viewModel.BackgroundImagePath = dialog.FileName;
            RefreshPreview();
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.BackgroundImagePath = null;
            RefreshPreview();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void RefreshPreview()
        {
            if (_viewModel.BackgroundImagePath == null)
            {
                PreviewImage.Source = null; // Image on a black Border background = "black" per spec
                return;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(_viewModel.BackgroundImagePath);
            bitmap.CacheOption = BitmapCacheOption.OnLoad; // load fully now, don't keep the file locked on disk
            bitmap.EndInit();

            PreviewImage.Source = bitmap;
        }
    }
}