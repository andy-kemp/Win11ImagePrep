using System;
using System.Threading;
using System.Windows;
using WinImagePrep.Models;

namespace WinImagePrep.Dialogs
{
    public partial class ProgressWindow : Window
    {
        private CancellationTokenSource? _cancellationTokenSource;
        public bool WasCancelled { get; private set; }

        public ProgressWindow(string title = "Processing...", string message = "Please wait...")
        {
            InitializeComponent();
            txtTitle.Text = title;
            txtMessage.Text = message;
        }

        public void SetCancellationTokenSource(CancellationTokenSource? cts)
        {
            _cancellationTokenSource = cts;
        }

        public void UpdateProgress(int percent, string? message = null)
        {
            Dispatcher.Invoke(() =>
            {
                progressBar.Value = percent;
                txtPercent.Text = $"{percent}%";

                if (!string.IsNullOrEmpty(message))
                {
                    txtMessage.Text = message;
                }
            });
        }

        public void UpdateProgress(OperationProgress progress)
        {
            Dispatcher.Invoke(() =>
            {
                progressBar.Value = progress.PercentComplete;
                txtPercent.Text = $"{progress.PercentComplete}%";
                txtMessage.Text = progress.CurrentOperation;
                progressBar.IsIndeterminate = progress.IsIndeterminate;
            });
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to cancel this operation?\n\nCancelling may leave the system in an inconsistent state.",
                "Confirm Cancellation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                WasCancelled = true;
                _cancellationTokenSource?.Cancel();
                btnCancel.IsEnabled = false;
                btnCancel.Content = "Cancelling...";
                txtMessage.Text = "Cancelling operation...";
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Prevent closing unless cancelled or completed
            if (!WasCancelled && progressBar.Value < 100)
            {
                e.Cancel = true;
            }
            base.OnClosing(e);
        }
    }
}
