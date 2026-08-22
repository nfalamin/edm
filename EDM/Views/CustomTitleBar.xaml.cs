using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EDM.Views
{
    public partial class CustomTitleBar : System.Windows.Controls.UserControl
    {
        private Window? _parentWindow;

        public CustomTitleBar()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            _parentWindow = Window.GetWindow(this);
            if (_parentWindow != null)
            {
                _parentWindow.StateChanged += ParentWindow_StateChanged;
                UpdateMaximizeRestoreIcon(_parentWindow.WindowState);
            }
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_parentWindow != null)
            {
                _parentWindow.StateChanged -= ParentWindow_StateChanged;
                _parentWindow = null;
            }
        }

        private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;

            if (!IsInteractiveElement(e.OriginalSource as DependencyObject))
            {
                var wnd = Window.GetWindow(this);
                if (wnd == null) return;

                try
                {
                    if (e.ClickCount == 2)
                    {
                        wnd.WindowState = wnd.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                    }
                    else
                    {
                        wnd.DragMove();
                    }
                }
                catch (Exception ex) { try { EDM.Services.LoggingService.LogException("[AutoFix] Swallowed exception in Root_MouseLeftButtonDown", ex); } catch { } }
            }
        }

        private static bool IsInteractiveElement(DependencyObject? source)
        {
            while (source != null)
            {
                if (source is System.Windows.Controls.Primitives.ButtonBase ||
                    source is System.Windows.Controls.TextBox ||
                    source is System.Windows.Controls.PasswordBox ||
                    source is System.Windows.Controls.ComboBox ||
                    source is System.Windows.Controls.Primitives.Thumb ||
                    source is System.Windows.Controls.Slider)
                {
                    return true;
                }
                source = System.Windows.Media.VisualTreeHelper.GetParent(source);
            }
            return false;
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            var wnd = Window.GetWindow(this);
            if (wnd != null) wnd.WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            var wnd = Window.GetWindow(this);
            if (wnd == null) return;
            wnd.WindowState = wnd.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            UpdateMaximizeRestoreIcon(wnd.WindowState);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            var wnd = Window.GetWindow(this);
            wnd?.Close();
        }

        private void ParentWindow_StateChanged(object? sender, EventArgs e)
        {
            if (_parentWindow != null)
            {
                UpdateMaximizeRestoreIcon(_parentWindow.WindowState);
            }
        }

        private void UpdateMaximizeRestoreIcon(WindowState state)
        {
            if (MaximizeButton == null) return;
            // Show restore icon when maximized, otherwise show maximize icon
            MaximizeButton.Content = state == WindowState.Maximized ? "❐" : "▢";
        }
    }
}
