using System.Collections.Generic;
using System.Linq;
using System.Windows;
using WinImagePrep.Models;

namespace WinImagePrep.Dialogs
{
    public partial class EditionSelectorWindow : Window
    {
        private List<WimEdition> _editions;
        public List<int> SelectedEditionIndices { get; private set; } = new();

        public EditionSelectorWindow(List<WimEdition> editions)
        {
            InitializeComponent();
            _editions = editions;
            lstEditions.ItemsSource = _editions;

            // Select all by default
            foreach (var edition in _editions)
            {
                edition.IsSelected = true;
            }
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var edition in _editions)
            {
                edition.IsSelected = true;
            }
        }

        private void BtnClearSelection_Click(object sender, RoutedEventArgs e)
        {
            foreach (var edition in _editions)
            {
                edition.IsSelected = false;
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            var selected = _editions.Where(e => e.IsSelected).ToList();

            if (selected.Count == 0)
            {
                MessageBox.Show(
                    "Please select at least one edition to process.\n\n" +
                    "Note: WinPE and Windows Setup will always be processed, but you must select at least one Windows edition.",
                    "No Edition Selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            SelectedEditionIndices = selected.Select(e => e.ImageIndex).ToList();

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
