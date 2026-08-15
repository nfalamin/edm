using System;

namespace EDM.Services
{
    public enum SegmentState
    {
        Pending,
        Downloading,
        Completed,
        Failed
    }

    public class SegmentRange
    {
        public int Id { get; set; }
        public long Start { get; set; }
        public long End { get; set; }
        public long BytesDownloaded { get; set; }
        public SegmentState State { get; set; } = SegmentState.Pending;
        public string? AssignedWorkerId { get; set; }
        public string TempPath { get; set; } = string.Empty;
        public string? Sha256Hash { get; set; }
        public int RetryCount { get; set; }

        public long TotalBytes => End - Start + 1;
        public long RemainingBytes => Math.Max(0, TotalBytes - BytesDownloaded);
        public bool IsCompleted => State == SegmentState.Completed || BytesDownloaded >= TotalBytes;

        public SegmentRange Clone()
        {
            return new SegmentRange
            {
                Id = Id,
                Start = Start,
                End = End,
                BytesDownloaded = BytesDownloaded,
                State = State,
                AssignedWorkerId = AssignedWorkerId,
                TempPath = TempPath,
                Sha256Hash = Sha256Hash,
                RetryCount = RetryCount
            };
        }
    }
}
