using System;

namespace EDM.Services.Interfaces
{
    public interface ITimeProvider
    {
        DateTime Now { get; }
        DateTime UtcNow { get; }
        DateTime Today { get; }
    }

    public class SystemTimeProvider : ITimeProvider
    {
        public static readonly SystemTimeProvider Instance = new();

        public DateTime Now => DateTime.Now;
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime Today => DateTime.Today;
    }
}
