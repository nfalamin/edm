using System.Windows;
using System.Windows.Controls;

namespace EDM.Views
{
    /// <summary>
    /// SystemStatusBar.xaml.cs - Footer widget bar displaying real-time network and download statistics
    /// Includes: Connection Status, Download Speed, Upload Speed, Active Downloads Count, Speed Limit Status
    /// </summary>
    public partial class SystemStatusBar : System.Windows.Controls.UserControl
    {
        public SystemStatusBar()
        {
            InitializeComponent();
        }

        #region Dependency Properties

        public string ConnectionStatusText
        {
            get { return (string)GetValue(ConnectionStatusTextProperty); }
            set { SetValue(ConnectionStatusTextProperty, value); }
        }
        public static readonly DependencyProperty ConnectionStatusTextProperty =
            DependencyProperty.Register("ConnectionStatusText", typeof(string), typeof(SystemStatusBar), 
                new PropertyMetadata("High Speed", OnConnectionStatusChanged));

        public string DownloadSpeedText
        {
            get { return (string)GetValue(DownloadSpeedTextProperty); }
            set { SetValue(DownloadSpeedTextProperty, value); }
        }
        public static readonly DependencyProperty DownloadSpeedTextProperty =
            DependencyProperty.Register("DownloadSpeedText", typeof(string), typeof(SystemStatusBar), 
                new PropertyMetadata("6.2 MB/s", OnDownloadSpeedChanged));

        public string UploadSpeedText
        {
            get { return (string)GetValue(UploadSpeedTextProperty); }
            set { SetValue(UploadSpeedTextProperty, value); }
        }
        public static readonly DependencyProperty UploadSpeedTextProperty =
            DependencyProperty.Register("UploadSpeedText", typeof(string), typeof(SystemStatusBar), 
                new PropertyMetadata("128.4 KB/s", OnUploadSpeedChanged));

        public string ActiveCountText
        {
            get { return (string)GetValue(ActiveCountTextProperty); }
            set { SetValue(ActiveCountTextProperty, value); }
        }
        public static readonly DependencyProperty ActiveCountTextProperty =
            DependencyProperty.Register("ActiveCountText", typeof(string), typeof(SystemStatusBar), 
                new PropertyMetadata("6 active", OnActiveCountChanged));

        public string SpeedLimitStatusText
        {
            get { return (string)GetValue(SpeedLimitStatusTextProperty); }
            set { SetValue(SpeedLimitStatusTextProperty, value); }
        }
        public static readonly DependencyProperty SpeedLimitStatusTextProperty =
            DependencyProperty.Register("SpeedLimitStatusText", typeof(string), typeof(SystemStatusBar), 
                new PropertyMetadata("Unlimited", OnSpeedLimitStatusChanged));

        #endregion

        #region Property Changed Handlers

        private static void OnConnectionStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as SystemStatusBar;
            if (control != null && control.ConnectionStatus != null)
            {
                control.ConnectionStatus.Text = (string)e.NewValue;
            }
        }

        private static void OnDownloadSpeedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as SystemStatusBar;
            if (control != null && control.DownloadSpeed != null)
            {
                control.DownloadSpeed.Text = (string)e.NewValue;
            }
        }

        private static void OnUploadSpeedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as SystemStatusBar;
            if (control != null && control.UploadSpeed != null)
            {
                control.UploadSpeed.Text = (string)e.NewValue;
            }
        }

        private static void OnActiveCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as SystemStatusBar;
            if (control != null && control.ActiveCount != null)
            {
                control.ActiveCount.Text = (string)e.NewValue;
            }
        }

        private static void OnSpeedLimitStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as SystemStatusBar;
            if (control != null && control.SpeedLimitStatus != null)
            {
                control.SpeedLimitStatus.Text = (string)e.NewValue;
            }
        }

        #endregion

        /// <summary>
        /// Update status bar with current real-time data (can be called from parent or service)
        /// </summary>
        public void UpdateStatus(string connection, string downSpeed, string upSpeed, string activeCount, string speedLimit)
        {
            ConnectionStatusText = connection;
            DownloadSpeedText = downSpeed;
            UploadSpeedText = upSpeed;
            ActiveCountText = activeCount;
            SpeedLimitStatusText = speedLimit;
        }
    }
}
