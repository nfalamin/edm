using System;

namespace EDM.Services
{
    public interface ISchedulerService : IDisposable
    {
        event Action? OnScheduleTriggered;
    }
}