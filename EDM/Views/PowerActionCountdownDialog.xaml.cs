using System;
using System.Windows;
using System.Windows.Threading;
using EDM.Services;

namespace EDM.Views
{
    public partial class PowerActionCountdownDialog : Window
    {
        private readonly PowerAction _action;
        private readonly DispatcherTimer _timer;
        private int _secondsRemaining;

        public bool WasCancelled { get; private set; }

        public PowerActionCountdownDialog(PowerAction action, int gracePeriodSeconds = 30)
        {
            InitializeComponent();
            _action = action;
            _secondsRemaining = gracePeriodSeconds;

            ActionTitleText.Text = $"⚡ System {_action} Scheduled";
            CountdownProgress.Maximum = gracePeriodSeconds;
            CountdownProgress.Value = gracePeriodSeconds;
            CountdownText.Text = $"{_secondsRemaining} seconds remaining...";

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += OnTimerTick;
            _timer.Start();
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            _secondsRemaining--;
            CountdownProgress.Value = _secondsRemaining;
            CountdownText.Text = $"{_secondsRemaining} seconds remaining...";

            if (_secondsRemaining <= 0)
            {
                _timer.Stop();
                DialogResult = true;
                Close();
            }
        }

        private void OnExecuteImmediately(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            DialogResult = true;
            Close();
        }

        private void OnCancelAction(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            WasCancelled = true;
            PowerActionScheduler.Instance.CancelCountdown();
            DialogResult = false;
            Close();
        }
    }
}
