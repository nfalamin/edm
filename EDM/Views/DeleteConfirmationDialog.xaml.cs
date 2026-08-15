using System.Windows;

namespace EDM.Views
{
    /// <summary>
    /// DeleteConfirmationDialog.xaml.cs - Modal dialog for confirming download deletion
    /// </summary>
    public partial class DeleteConfirmationDialog : Window
    {
        private string _fileName = "this file";

        public string FileName
        {
            get => _fileName;
            set
            {
                _fileName = value;
                UpdateFileNameDisplay();
            }
        }

        public DeleteConfirmationDialog()
        {
            InitializeComponent();
        }

        private void UpdateFileNameDisplay()
        {
            if (!string.IsNullOrWhiteSpace(_fileName))
            {
                FileNameTextBlock.Text = $"Delete '{System.IO.Path.GetFileName(_fileName)}'?";
            }
        }

        /// <summary>
        /// Close button click - returns false (Cancel/No)
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        /// <summary>
        /// Cancel button click - returns false
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        /// <summary>
        /// Delete button click - returns true
        /// </summary>
        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }
    }
}
