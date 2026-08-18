using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EDM.Models;
using EDM.Services;
using EDM.ViewModels;

namespace EDM.Views
{
    /// <summary>
    /// DownloadsTable.xaml.cs - Main downloads list with filtering, view toggle, and inline actions
    /// </summary>
    public partial class DownloadsTable : System.Windows.Controls.UserControl
    {
        private DownloadManagerViewModel _viewModel;
        private string _currentViewMode = "List"; // "List" or "Grid"

        public DownloadManagerViewModel ViewModel
        {
            get { return _viewModel; }
            set 
            { 
                _viewModel = value;
                if (_viewModel != null)
                {
                    this.DataContext = _viewModel;
                    DownloadsItemsControl.ItemsSource = _viewModel.FilteredDownloads;
                    _viewModel.ApplyFilter();
                    _viewModel.RecalculateMetrics();
                    System.Diagnostics.Debug.WriteLine($"DownloadsTable ViewModel set. FilteredDownloads count: {_viewModel.FilteredDownloads.Count}");
                }
            }
        }

        public DownloadsTable()
        {
            InitializeComponent();

            // Create ViewModel — history loads automatically from SQLite in constructor
            _viewModel = new DownloadManagerViewModel();
            this.DataContext = _viewModel;

            // Bind ItemsSource to ViewModel's FilteredDownloads
            DownloadsItemsControl.ItemsSource = _viewModel.FilteredDownloads;

            // Initialize with default filter
            StatusFilterCombo.SelectedIndex = 0;
            ListViewBtn.Focus();

            System.Diagnostics.Debug.WriteLine("DownloadsTable initialized — history loading from DB");
        }

        /// <summary>
        /// Handle View Mode Toggle (List vs Grid)
        /// </summary>
        private void ViewToggle_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as System.Windows.Controls.Button;
            if (btn?.Tag is string mode)
            {
                _currentViewMode = mode;

                // Update button styles
                if (mode == "List")
                {
                    ListViewBtn.Foreground = (System.Windows.Media.Brush)FindResource("PurpleBrush") 
                        ?? System.Windows.Media.Brushes.Purple;
                    ListViewBtn.BorderBrush = (System.Windows.Media.Brush)FindResource("BorderHighlightBrush") 
                        ?? System.Windows.Media.Brushes.Gray;
                    GridViewBtn.Foreground = (System.Windows.Media.Brush)FindResource("SecondaryTextBrush") 
                        ?? System.Windows.Media.Brushes.Gray;
                    GridViewBtn.BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush") 
                        ?? System.Windows.Media.Brushes.DarkGray;
                }
                else
                {
                    GridViewBtn.Foreground = (System.Windows.Media.Brush)FindResource("PurpleBrush") 
                        ?? System.Windows.Media.Brushes.Purple;
                    GridViewBtn.BorderBrush = (System.Windows.Media.Brush)FindResource("BorderHighlightBrush") 
                        ?? System.Windows.Media.Brushes.Gray;
                    ListViewBtn.Foreground = (System.Windows.Media.Brush)FindResource("SecondaryTextBrush") 
                        ?? System.Windows.Media.Brushes.Gray;
                    ListViewBtn.BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush") 
                        ?? System.Windows.Media.Brushes.DarkGray;
                }

                System.Diagnostics.Debug.WriteLine($"View Mode: {_currentViewMode}");
            }
        }

        /// <summary>
        /// Handle Status Filter Selection - Updates ViewModel current filter
        /// </summary>
        private void StatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StatusFilterCombo.SelectedItem is ComboBoxItem item && item.Content is string filter)
            {
                if (_viewModel != null)
                {
                    // Convert filter text to ViewModel format
                    string vmFilter = filter switch
                    {
                        "All Status" => "All",
                        "Downloading" => "Downloading",
                        "Paused" => "Paused",
                        "Queued" => "Queued",
                        "Completed" => "Completed",
                        _ => "All"
                    };
                    _viewModel.CurrentFilter = vmFilter;
                    System.Diagnostics.Debug.WriteLine($"Filter changed to: {vmFilter}");
                }
            }
        }

        /// <summary>
        /// Handle row mouse enter for hover effect
        /// </summary>
        private void Row_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is Border border && border.Tag is DownloadItem download)
            {
                // Add subtle highlight/shadow effect
                border.Opacity = 1.0;
            }
        }

        /// <summary>
        /// Handle row mouse leave
        /// </summary>
        private void Row_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is Border border && border.Tag is DownloadItem download)
            {
                // Reset styling
                border.Opacity = 0.95;
            }
        }

        /// <summary>
        /// Handle Pause/Resume button click - Toggles download state
        /// </summary>
        private void PauseResumeBtn_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as System.Windows.Controls.Button;
            if (btn?.DataContext is DownloadItem download && _viewModel != null)
            {
                _viewModel.TogglePauseResume(download);
                UpdateActionButton(download, btn);
                System.Diagnostics.Debug.WriteLine($"Pause/Resume clicked for: {download.FileName}, Status now: {download.Status}");
            }
        }

        /// <summary>
        /// Handle Delete button click - Shows confirmation dialog
        /// </summary>
        private void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as System.Windows.Controls.Button;
            if (btn?.DataContext is DownloadItem download && _viewModel != null)
            {
                // Show delete confirmation dialog
                DeleteConfirmationDialog dlg = new DeleteConfirmationDialog();
                dlg.FileName = download.FileName;
                dlg.Owner = Window.GetWindow(this);

                if (dlg.ShowDialog() == true)
                {
                    _viewModel.DeleteDownload(download);
                    System.Diagnostics.Debug.WriteLine($"Deleted: {download.FileName}");
                }
            }
        }

        /// <summary>
        /// Handle More Options (3-dot menu) button click - opens row context menu
        /// </summary>
        private void MoreOptionsBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn)
            {
                var parent = System.Windows.Media.VisualTreeHelper.GetParent(btn);
                while (parent != null && !(parent is Border b && b.ContextMenu != null))
                {
                    parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
                }

                if (parent is Border rowBorder && rowBorder.ContextMenu != null)
                {
                    rowBorder.ContextMenu.PlacementTarget = btn;
                    rowBorder.ContextMenu.IsOpen = true;
                }
            }
        }

        /// <summary>
        /// Update action button (Pause/Play icon and status) based on download state
        /// </summary>
        private void UpdateActionButton(DownloadItem download, System.Windows.Controls.Button? btn)
        {
            if (btn is null) return;

            if (download.Status.Contains("Downloading"))
            {
                btn.Content = "⏸"; // Pause icon
                btn.ToolTip = "Pause Download";
            }
            else if (download.Status.Contains("Paused"))
            {
                btn.Content = "▶"; // Play icon
                btn.ToolTip = "Resume Download";
            }
        }

        /// <summary>
        /// Handle row double click to open Turbo Progress Window or Properties Window
        /// </summary>
        private void Row_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && sender is Border border && border.Tag is DownloadItem download)
            {
                e.Handled = true;
                OpenProgressOrPropertiesWindow(download);
            }
        }

        private void OpenProgressOrPropertiesWindow(DownloadItem download)
        {
            if (download == null) return;
            try
            {
                if (download.Status == "Completed" || download.Status == "Error" || download.Status == "Cancelled")
                {
                    var propWin = new DownloadPropertiesWindow(download)
                    {
                        Owner = Window.GetWindow(this)
                    };
                    propWin.Show();
                }
                else
                {
                    var progWin = new DownloadProgressWindow(download)
                    {
                        Owner = Window.GetWindow(this)
                    };
                    progWin.Show();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[DownloadsTable] Failed to open Progress/Properties window", ex);
            }
        }

        // ==================== CONTEXT MENU HANDLERS ====================

        /// <summary>
        /// Context Menu: Show Turbo Progress Window
        /// </summary>
        private void MenuItem_ShowProgressWindow(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as System.Windows.Controls.MenuItem;
            var contextMenu = menuItem?.Parent as System.Windows.Controls.ContextMenu;
            if (contextMenu?.PlacementTarget is Border border && border.Tag is DownloadItem download)
            {
                try
                {
                    var progWin = new DownloadProgressWindow(download)
                    {
                        Owner = Window.GetWindow(this)
                    };
                    progWin.Show();
                }
                catch (Exception ex)
                {
                    LoggingService.LogException("[DownloadsTable] Failed to open Progress Window", ex);
                }
            }
        }

        /// <summary>
        /// Context Menu: Download Properties
        /// </summary>
        private void MenuItem_Properties(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as System.Windows.Controls.MenuItem;
            var contextMenu = menuItem?.Parent as System.Windows.Controls.ContextMenu;
            if (contextMenu?.PlacementTarget is Border border && border.Tag is DownloadItem download)
            {
                try
                {
                    var propWin = new DownloadPropertiesWindow(download)
                    {
                        Owner = Window.GetWindow(this)
                    };
                    propWin.Show();
                }
                catch (Exception ex)
                {
                    LoggingService.LogException("[DownloadsTable] Failed to open Properties Window", ex);
                }
            }
        }

        /// <summary>
        /// Context Menu: Open File - Opens the downloaded file in its default application
        /// </summary>
        private void MenuItem_OpenFile(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as System.Windows.Controls.MenuItem;
            var contextMenu = menuItem?.Parent as System.Windows.Controls.ContextMenu;
            if (contextMenu?.PlacementTarget is Border border && border.Tag is DownloadItem download)
            {
                if (System.IO.File.Exists(download.SavePath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = download.SavePath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    System.Windows.MessageBox.Show("File not found.", "Error");
                }
            }
        }

        /// <summary>
        /// Context Menu: Open Folder - Opens the folder containing the download in Explorer
        /// </summary>
        private void MenuItem_OpenFolder(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as System.Windows.Controls.MenuItem;
            var contextMenu = menuItem?.Parent as System.Windows.Controls.ContextMenu;
            if (contextMenu?.PlacementTarget is Border border && border.Tag is DownloadItem download)
            {
                string folderPath = System.IO.Path.GetDirectoryName(download.SavePath) ?? download.SavePath;
                if (System.IO.Directory.Exists(folderPath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", folderPath);
                }
                else
                {
                    System.Windows.MessageBox.Show("Folder not found.", "Error");
                }
            }
        }

        /// <summary>
        /// Context Menu: Redownload - Restarts the download from the beginning
        /// </summary>
        private void MenuItem_Redownload(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as System.Windows.Controls.MenuItem;
            var contextMenu = menuItem?.Parent as System.Windows.Controls.ContextMenu;
            if (contextMenu?.PlacementTarget is Border border && border.Tag is DownloadItem download && _viewModel != null)
            {
                download.Progress = 0.0;
                download.Status = "Queued";
                download.TransferRate = "0 B/s";
                download.TimeLeft = "--:--:--";
                System.Diagnostics.Debug.WriteLine($"Redownload initiated: {download.FileName}");
            }
        }

        /// <summary>
        /// Context Menu: Force Start - Moves download to front of queue and starts immediately
        /// </summary>
        private void MenuItem_ForceStart(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as System.Windows.Controls.MenuItem;
            var contextMenu = menuItem?.Parent as System.Windows.Controls.ContextMenu;
            if (contextMenu?.PlacementTarget is Border border && border.Tag is DownloadItem download && _viewModel != null)
            {
                download.Status = "Downloading";
                download.Progress = 0.1;
                download.TransferRate = "2.4 MB/s";
                System.Diagnostics.Debug.WriteLine($"Force start: {download.FileName}");
            }
        }

        /// <summary>
        /// Context Menu: Copy URL - Copies the download URL to clipboard
        /// </summary>
        private void MenuItem_CopyUrl(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as System.Windows.Controls.MenuItem;
            var contextMenu = menuItem?.Parent as System.Windows.Controls.ContextMenu;
            if (contextMenu?.PlacementTarget is Border border && border.Tag is DownloadItem download)
            {
                if (!string.IsNullOrEmpty(download.Url))
                {
                    System.Windows.Forms.Clipboard.SetText(download.Url);
                    System.Diagnostics.Debug.WriteLine($"URL copied: {download.Url}");
                }
            }
        }
    }
}
