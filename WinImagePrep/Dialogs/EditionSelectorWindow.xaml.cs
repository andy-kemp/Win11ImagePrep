using System.Collections.Generic;
using System.Linq;
using System.Windows;
using WinImagePrep.Models;

namespace WinImagePrep.Dialogs
{
    public partial class EditionSelectorWindow : Window
    {
        public List<int> SelectedEditionIndices { get; private set; } = new();

        public EditionSelectorWindow(List<WimEdition> editions)
        {
            InitializeComponent();
            lstEditions.ItemsSource = editions;

            // Select all by default
            lstEditions.SelectAll();
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            lstEditions.SelectAll();
        }

        private void BtnClearSelection_Click(object sender, RoutedEventArgs e)
        {
            lstEditions.SelectedItems.Clear();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (lstEditions.SelectedItems.Count == 0)
            {
                MessageBox.Show(
                    "Please select at least one edition to process.",
                    "No Selection",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            SelectedEditionIndices = lstEditions.SelectedItems
                .Cast<WimEdition>()
                .Select(e => e.ImageIndex)
                .ToList();

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
