using System.Windows;
using WinImagePrep.ViewModels;

namespace WinImagePrep
{
    /// <summary>
    /// Options window for configuring application settings
    /// </summary>
    public partial class OptionsWindow : Window
    {
        private readonly OptionsViewModel _viewModel;

        public OptionsWindow(OptionsViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
        }

        /// <summary>
        /// Handles Save button click
        /// </summary>
        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            // Call the SaveAsync method directly
            var saved = await _viewModel.SaveAsync();
            if (saved)
            {
                DialogResult = true;
                Close();
            }
        }

        /// <summary>
        /// Handles Cancel button click
        /// </summary>
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            // Check if there are unsaved changes
            if (_viewModel.HasChanges())
            {
                var result = MessageBox.Show(
                    "You have unsaved changes. Are you sure you want to cancel?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;
            }

            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Handles window closing
        /// </summary>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // If user clicks X button and there are unsaved changes
            if (DialogResult == null && _viewModel.HasChanges())
            {
                var result = MessageBox.Show(
                    "You have unsaved changes. Are you sure you want to close?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }

                DialogResult = false;
            }

            base.OnClosing(e);
        }
    }
}
