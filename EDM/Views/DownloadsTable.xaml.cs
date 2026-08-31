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
        /// Context Menu: Resume / Start - Resumes a paused download or starts a queued one
        /// </summary>
        private void MenuItem_Resume(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as System.Windows.Controls.MenuItem;
            var contextMenu = menuItem?.Parent as System.Windows.Controls.ContextMenu;
            if (contextMenu?.PlacementTarget is Border border && border.Tag is DownloadItem download && _viewModel != null)
            {
                if (string.Equals(download.Status, "Paused", StringComparison.OrdinalIgnoreCase))
                {
                    download.PauseSource.Resume();
                    download.Status = "Downloading";
                }
                else if (download.Status == "Queued" || download.Status == "Stopped" || download.Status == "Error")
                {
                    download.Status = "Downloading";
                    _ = _viewModel.StartDownloadProcessAsync(download);
                }
            }
        }

        /// <summary>
        /// Context Menu: Pause - Pauses an active download
        /// </summary>
        private void MenuItem_Pause(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as System.Windows.Controls.MenuItem;
            var contextMenu = menuItem?.Parent as System.Windows.Controls.ContextMenu;
            if (contextMenu?.PlacementTarget is Border border && border.Tag is DownloadItem download && _viewModel != null)
            {
                if (download.Status != null && download.Status.Contains("Downloading", StringComparison.OrdinalIgnoreCase))
                {
                    download.PauseSource.Pause();
                    download.Status = "Paused";
                    download.TransferRate = "0 B/s";
                }
            }
        }

        /// <summary>
        /// Context Menu: Cancel / Stop - Stops the active download task
        /// </summary>
        private void MenuItem_Cancel(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as System.Windows.Controls.MenuItem;
            var contextMenu = menuItem?.Parent as System.Windows.Controls.ContextMenu;
            if (contextMenu?.PlacementTarget is Border border && border.Tag is DownloadItem download)
            {
                download.CancelAndReset();
                download.Status = "Cancelled";
                download.TransferRate = "0 B/s";
                _viewModel?.RecalculateMetrics();
            }
        }

        /// <summary>
        /// Context Menu: Delete - Prompts user and deletes download
        /// </summary>
        private void MenuItem_Delete(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as System.Windows.Controls.MenuItem;
            var contextMenu = menuItem?.Parent as System.Windows.Controls.ContextMenu;
            if (contextMenu?.PlacementTarget is Border border && border.Tag is DownloadItem download && _viewModel != null)
            {
                var dlg = new DeleteConfirmationDialog
                {
                    FileName = download.FileName,
                    Owner = Window.GetWindow(this)
                };

                if (dlg.ShowDialog() == true)
                {
                    _viewModel.DeleteDownload(download);
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
                download.DownloadedBytes = 0;
                download.Status = "Downloading";
                download.TransferRate = "0 B/s";
                download.TimeLeft = "Calculating...";
                _ = _viewModel.StartDownloadProcessAsync(download);
                System.Diagnostics.Debug.WriteLine($"Redownload initiated: {download.FileName}");
            }
        }

        /// <summary>
        /// Context Menu: Refresh Download Address - Updates the download URL for expired links and resumes
        /// </summary>
        private void MenuItem_RefreshAddress(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as System.Windows.Controls.MenuItem;
            var contextMenu = menuItem?.Parent as System.Windows.Controls.ContextMenu;
            if (contextMenu?.PlacementTarget is Border border && border.Tag is DownloadItem download)
            {
                try
                {
                    var refreshWin = new RefreshAddressWindow(download, _viewModel);
                    refreshWin.Owner = Window.GetWindow(this);
                    refreshWin.ShowDialog();
                }
                catch (Exception ex)
                {
                    LoggingService.LogException("[DownloadsTable] MenuItem_RefreshAddress failed", ex);
                }
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
                download.PauseSource.Resume();
                download.Status = "Downloading";
                download.TransferRate = "0 B/s";
                _ = _viewModel.StartDownloadProcessAsync(download);
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
                    try
                    {
                        System.Windows.Clipboard.SetText(download.Url);
                        System.Diagnostics.Debug.WriteLine($"URL copied: {download.Url}");
                    }
                    catch (Exception ex)
                    {
                        LoggingService.LogException("[DownloadsTable] Failed to copy URL to clipboard", ex);
                    }
                }
            }
        }

        /// <summary>
        /// Keyboard accessibility shortcuts for list items
        /// </summary>
        private void DownloadsItemsControl_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (DownloadsItemsControl.SelectedItem is DownloadItem selected && _viewModel != null)
            {
                if (e.Key == Key.Delete)
                {
                    var dlg = new DeleteConfirmationDialog
                    {
                        FileName = selected.FileName,
                        Owner = Window.GetWindow(this)
                    };
                    if (dlg.ShowDialog() == true)
                    {
                        _viewModel.DeleteDownload(selected);
                    }
                    e.Handled = true;
                }
                else if (e.Key == Key.Space)
                {
                    _viewModel.TogglePauseResume(selected);
                    e.Handled = true;
                }
                else if (e.Key == Key.Enter)
                {
                    if (string.Equals(selected.Status, "Completed", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrEmpty(selected.SavePath) && System.IO.File.Exists(selected.SavePath))
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(selected.SavePath) { UseShellExecute = true });
                        }
                        catch { }
                    }
                    else
                    {
                        var propWin = new DownloadPropertiesWindow(selected);
                        propWin.Owner = Window.GetWindow(this);
                        propWin.ShowDialog();
                    }
                    e.Handled = true;
                }
                else if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    if (!string.IsNullOrEmpty(selected.Url))
                    {
                        try
                        {
                            System.Windows.Clipboard.SetText(selected.Url);
                        }
                        catch { }
                    }
                    e.Handled = true;
                }
            }
        }
    }
}
