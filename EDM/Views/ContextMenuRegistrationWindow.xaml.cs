using System;
using System.Windows;
using EDM.Services;

namespace EDM.Views
{
    /// <summary>
    /// Confirmation dialog for registering/unregistering Windows Explorer context menu.
    /// </summary>
    public partial class ContextMenuRegistrationWindow : Window
    {
        private bool _isRegister = true;

        public ContextMenuRegistrationWindow(bool isRegister = true)
        {
            InitializeComponent();
            _isRegister = isRegister;

            // Update UI based on action
            if (_isRegister)
            {
                Title = "Enable Context Menu";
                ConfirmButton.Content = "Enable";
            }
            else
            {
                Title = "Disable Context Menu";
                ConfirmButton.Content = "Disable";
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ContextMenuService.ContextMenuResult result;

                if (_isRegister)
                {
                    result = ContextMenuService.RegisterContextMenu();
                }
                else
                {
                    result = ContextMenuService.UnregisterContextMenu();
                }

                if (!result.Success)
                {
                    // Check if elevation is needed
                    if (result.Message.Contains("Administrative"))
                    {
                        System.Windows.MessageBox.Show(
                            result.Message,
                            "Admin Privileges Required",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }

                    System.Windows.MessageBox.Show(
                        result.Message,
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                System.Windows.MessageBox.Show(
                    result.Message,
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[ContextMenuRegistrationWindow]", ex);
                System.Windows.MessageBox.Show(
                    $"An error occurred: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
