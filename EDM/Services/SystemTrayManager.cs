using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Forms;

namespace EDM.Services
{
    /// <summary>
    /// System Tray Icon Manager for EDM.
    /// Provides notify icon support in the Windows taskbar system tray:
    ///   - Minimise to tray on main window minimize / close
    ///   - Context menu: Open EDM, Pause All, Resume All, Exit
    ///   - Download completion balloon tip / toast notification
    /// </summary>
    public sealed class SystemTrayManager : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly Window _mainWindow;
        private bool _disposed;

        public event Action? OnPauseAllRequested;
        public event Action? OnResumeAllRequested;

        public SystemTrayManager(Window mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));

            _notifyIcon = new NotifyIcon
            {
                Text = "Exclusive Download Manager (EDM)",
                Visible = true,
                Icon = SystemIcons.Application
            };

            // Try to load application icon from exe directory if available
            try
            {
                string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    var extracted = Icon.ExtractAssociatedIcon(exePath);
                    if (extracted != null) _notifyIcon.Icon = extracted;
                }
            }
            catch (Exception ex) { try { LoggingService.LogException("[AutoFix] Swallowed exception extracting icon in SystemTrayManager", ex); } catch { } }

            // Context Menu
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Open EDM", null, (s, e) => ShowMainWindow());
            contextMenu.Items.Add(new ToolStripSeparator());

            var settingsService = (App.ServiceProvider?.GetService(typeof(Interfaces.ISettingsService)) as Interfaces.ISettingsService)
                ?? new SettingsService();

            var clipboardMenuItem = new ToolStripMenuItem("Monitor Clipboard")
            {
                CheckOnClick = true,
                Checked = settingsService.GetEnableClipboardMonitoring()
            };
            clipboardMenuItem.Click += (s, e) =>
            {
                bool newState = clipboardMenuItem.Checked;
                settingsService.SetEnableClipboardMonitoring(newState);
                ShowNotification("EDM Clipboard Monitor", newState ? "Clipboard monitoring enabled ✓" : "Clipboard monitoring disabled", ToolTipIcon.Info);
            };
            contextMenu.Items.Add(clipboardMenuItem);

            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("Pause All Downloads", null, (s, e) => OnPauseAllRequested?.Invoke());
            contextMenu.Items.Add("Resume All Downloads", null, (s, e) => OnResumeAllRequested?.Invoke());
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("Exit", null, (s, e) => ExitApplication());

            _notifyIcon.ContextMenuStrip = contextMenu;

            // Double click tray icon restores main window
            _notifyIcon.DoubleClick += (s, e) => ShowMainWindow();
        }

        public void ShowMainWindow()
        {
            if (_mainWindow.CheckAccess())
            {
                _mainWindow.Show();
                _mainWindow.WindowState = WindowState.Normal;
                _mainWindow.Activate();
            }
            else
            {
                _mainWindow.Dispatcher.Invoke(ShowMainWindow);
            }
        }

        public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
        {
            try
            {
                _notifyIcon.ShowBalloonTip(3000, title, message, icon);
            }
            catch (Exception ex)
            {
                LoggingService.LogException("[SystemTrayManager] ShowNotification failed", ex);
            }
        }

        public void ShowDownloadCompletedNotification(string fileName, string savePath)
        {
            ShowNotification("Download Completed ✓", $"{fileName}\nSaved to: {savePath}", ToolTipIcon.Info);
        }

        private void ExitApplication()
        {
            Dispose();
            System.Windows.Application.Current.Shutdown();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            catch (Exception ex) { try { LoggingService.LogException("[AutoFix] Swallowed exception in SystemTrayManager.Dispose", ex); } catch { } }
        }
    }
}
