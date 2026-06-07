using System.Windows;

namespace WinImagePrep.Dialogs
{
    /// <summary>
    /// Dialog for prompting user about available updates with option to disable automatic checks
    /// </summary>
    public partial class UpdatePromptDialog : Window
    {
        public bool UpdateNow { get; private set; }
        public bool DontAskAgain { get; private set; }

        public UpdatePromptDialog(string message)
        {
            InitializeComponent();
            MessageText.Text = message;
        }

        private void UpdateNow_Click(object sender, RoutedEventArgs e)
        {
            UpdateNow = true;
            DontAskAgain = DontAskAgainCheckBox.IsChecked == true;
            DialogResult = true;
            Close();
        }

        private void Later_Click(object sender, RoutedEventArgs e)
        {
            UpdateNow = false;
            DontAskAgain = DontAskAgainCheckBox.IsChecked == true;
            DialogResult = false;
            Close();
        }
    }
}
