using System;
using System.Windows;

namespace EDM.Views
{
    public partial class SchedulerWindow : Window
    {
        public bool IsSchedulerEnabled { get; private set; }
        public TimeSpan ScheduledTime { get; private set; }

        public SchedulerWindow(bool isCurrentlyActive = false, TimeSpan? currentScheduledTime = null)
        {
            InitializeComponent();
            IsSchedulerEnabled = isCurrentlyActive;
            if (currentScheduledTime.HasValue)
            {
                ScheduledTime = currentScheduledTime.Value;
            }
        }

        public SchedulerWindow() : this(false, null)
        {
        }
    }
}