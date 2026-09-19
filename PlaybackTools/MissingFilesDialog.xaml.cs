using System.Collections.Generic;
using System.Windows;

namespace PlaybackTools
{
    public partial class MissingFilesDialog : Window
    {
        public MissingFilesDialog(IReadOnlyList<string> missingPaths)
        {
            InitializeComponent();
            FileListTextBlock.Text = string.Join("\n\n", missingPaths);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}