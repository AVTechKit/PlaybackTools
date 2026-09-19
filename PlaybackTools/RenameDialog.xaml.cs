using System.Windows;

namespace PlaybackTools
{
    public partial class RenameDialog : Window
    {
        public string ResultText { get; private set; } = "";

        public RenameDialog(string currentName)
        {
            InitializeComponent();
            NameTextBox.Text = currentName;
            NameTextBox.SelectAll();
            NameTextBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ResultText = NameTextBox.Text;
            DialogResult = true;
        }
    }
}