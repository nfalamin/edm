using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using EDM.Services;

namespace EDM.Views
{
    public class SiteCredentialViewModel
    {
        public string Hostname { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string MaskedPassword => "••••••••";
    }

    public partial class SiteLoginsManagerWindow : Window
    {
        public ObservableCollection<SiteCredentialViewModel> Credentials { get; set; } = new();

        public SiteLoginsManagerWindow()
        {
            InitializeComponent();
            CredentialsGrid.ItemsSource = Credentials;
            LoadCredentials();
        }

        private void LoadCredentials()
        {
            Credentials.Clear();
            var saved = SecureCredentialVault.GetAllCredentials();
            foreach (var cred in saved)
            {
                Credentials.Add(new SiteCredentialViewModel { Hostname = cred.Host, Username = cred.Username });
            }
        }

        private void OnAddCredential(object sender, RoutedEventArgs e)
        {
            string host = SiteBox.Text.Trim();
            string user = UserBox.Text.Trim();
            string pass = PassBox.Password;

            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
            {
                System.Windows.MessageBox.Show("Please provide Host, Username, and Password.", "EDM", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Encrypt using DPAPI
            SecureCredentialVault.SaveCredentials(host, user, pass);

            Credentials.Add(new SiteCredentialViewModel { Hostname = host, Username = user });
            SiteBox.Clear();
            UserBox.Clear();
            PassBox.Clear();
        }

        private void OnDeleteSelected(object sender, RoutedEventArgs e)
        {
            if (CredentialsGrid.SelectedItem is SiteCredentialViewModel selected)
            {
                SecureCredentialVault.DeleteCredentials(selected.Hostname);
                Credentials.Remove(selected);
            }
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
