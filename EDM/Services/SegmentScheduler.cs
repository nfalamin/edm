using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;


namespace EDM.Services
{
    public class SegmentScheduler
    {
        private readonly object _lock = new();
        private readonly List<SegmentRange> _segments = new();
        private readonly long _totalBytes;
        private readonly long _minSplitThresholdBytes;
        private int _nextSegmentId = 0;

        public long TotalBytes => _totalBytes;
        public IReadOnlyList<SegmentRange> Segments => GetSegmentsSnapshot();

        public SegmentScheduler(long totalBytes, long minSplitThresholdBytes = 2 * 1024 * 1024)
        {
            if (totalBytes <= 0) throw new ArgumentOutOfRangeException(nameof(totalBytes));
            _totalBytes = totalBytes;
            _minSplitThresholdBytes = Math.Max(1, minSplitThresholdBytes);
        }

        public void InitializeFromState(IEnumerable<SegmentRange> initialSegments)
        {
            lock (_lock)
            {
                _segments.Clear();
                _segments.AddRange(initialSegments.Select(s => s.Clone()));
                _nextSegmentId = _segments.Count > 0 ? _segments.Max(s => s.Id) + 1 : 0;
            }
        }

        public void InitializeDefault(int initialSegmentCount)
        {
            if (initialSegmentCount <= 0) initialSegmentCount = 1;
            lock (_lock)
            {
                _segments.Clear();
                long baseSize = _totalBytes / initialSegmentCount;
                long remainder = _totalBytes % initialSegmentCount;
                long offset = 0;

                for (int i = 0; i < initialSegmentCount; i++)
                {
                    long size = baseSize + (i < remainder ? 1 : 0);
                    if (size <= 0) continue;

                    long start = offset;
                    long end = offset + size - 1;
                    _segments.Add(new SegmentRange
                    {
                        Id = i,
                        Start = start,
                        End = end,
                        BytesDownloaded = 0,
                        State = SegmentState.Pending
                    });
                    offset += size;
                }
                _nextSegmentId = _segments.Count;
            }
        }

        private readonly ConcurrentDictionary<string, WorkerPerformanceInfo> _workerTelemetry = new();

        public class WorkerPerformanceInfo
        {
            public string WorkerId { get; set; } = string.Empty;
            public int CurrentSegmentId { get; set; }
            public long BytesDownloaded { get; set; }
            public double SpeedBps { get; set; }
            public DateTime LastActivity { get; set; } = DateTime.UtcNow;
            public bool IsStalled => (DateTime.UtcNow - LastActivity).TotalSeconds > 3.0;
        }

        public void RegisterWorkerProgress(string workerId, int segmentId, long bytesDownloaded, double speedBps)
        {
            if (string.IsNullOrEmpty(workerId)) return;
            var info = _workerTelemetry.GetOrAdd(workerId, id => new WorkerPerformanceInfo { WorkerId = id });
            info.CurrentSegmentId = segmentId;
            info.BytesDownloaded = bytesDownloaded;
            info.SpeedBps = speedBps;
            info.LastActivity = DateTime.UtcNow;
        }

        public SegmentRange? GetNextWorkItem(string workerId)
        {
            lock (_lock)
            {
                // 1. Prefer unassigned Pending segments
                var pending = _segments.FirstOrDefault(s => s.State == SegmentState.Pending);
                if (pending != null)
                {
                    pending.State = SegmentState.Downloading;
                    pending.AssignedWorkerId = workerId;
                    return pending.Clone();
                }

                // 2. Dynamic Work Stealing: Prioritize slowest or stalled workers with large remaining ranges,
                // or largest active Downloading segment with remaining bytes >= 2 * minSplitThreshold
                var candidate = _segments
                    .Where(s => s.State == SegmentState.Downloading && s.RemainingBytes >= _minSplitThresholdBytes * 2)
                    .OrderByDescending(s => s.RemainingBytes)
                    .FirstOrDefault();

                if (candidate != null)
                {
                    long currentPos = candidate.Start + candidate.BytesDownloaded;
                    long remainingFromCurrent = candidate.End - currentPos;
                    if (remainingFromCurrent >= _minSplitThresholdBytes * 2)
                    {
                        long half = remainingFromCurrent / 2;
                        long alignment = Math.Min(64 * 1024, _minSplitThresholdBytes);
                        if (alignment > 0) half = (half / alignment) * alignment;
                        if (half < _minSplitThresholdBytes) half = _minSplitThresholdBytes;

                        long splitPoint = candidate.End - half;
                        if (splitPoint > currentPos + alignment)
                        {
                            long oldEnd = candidate.End;
                            candidate.End = splitPoint;
                            string candidateDir = !string.IsNullOrEmpty(candidate.TempPath) ? (Path.GetDirectoryName(candidate.TempPath) ?? "") : "";
                            string stolenPath = !string.IsNullOrEmpty(candidateDir) ? Path.Combine(candidateDir, $"segment_{_nextSegmentId}.part") : string.Empty;

                            var newSegment = new SegmentRange
                            {
                                Id = _nextSegmentId++,
                                Start = splitPoint + 1,
                                End = oldEnd,
                                BytesDownloaded = 0,
                                State = SegmentState.Downloading,
                                AssignedWorkerId = workerId,
                                TempPath = stolenPath
                            };
                            _segments.Add(newSegment);
                            return newSegment.Clone();
                        }
                    }
                }

                return null;
            }
        }

        public long GetAssignedEnd(int segmentId)
        {
            lock (_lock)
            {
                var seg = _segments.FirstOrDefault(s => s.Id == segmentId);
                return seg?.End ?? long.MaxValue;
            }
        }

        public void ReportProgress(int segmentId, long bytesDownloaded)
        {
            lock (_lock)
            {
                var seg = _segments.FirstOrDefault(s => s.Id == segmentId);
                if (seg != null)
                {
                    seg.BytesDownloaded = Math.Min(seg.TotalBytes, Math.Max(0, bytesDownloaded));
                    if (seg.BytesDownloaded >= seg.TotalBytes)
                    {
                        seg.State = SegmentState.Completed;
                    }
                }
            }
        }

        public void UpdateTempPath(int segmentId, string tempPath)
        {
            if (string.IsNullOrEmpty(tempPath)) return;
            lock (_lock)
            {
                var seg = _segments.FirstOrDefault(s => s.Id == segmentId);
                if (seg != null && (string.IsNullOrEmpty(seg.TempPath) || seg.TempPath != tempPath))
                {
                    seg.TempPath = tempPath;
                }
            }
        }

        public bool MarkCompleted(int segmentId)

        {
            lock (_lock)
            {
                var seg = _segments.FirstOrDefault(s => s.Id == segmentId);
                if (seg != null && seg.State != SegmentState.Completed)
                {
                    seg.BytesDownloaded = seg.TotalBytes;
                    seg.State = SegmentState.Completed;
                    seg.AssignedWorkerId = null;
                    return true;
                }
                return false;
            }
        }

        public bool MarkFailed(int segmentId, bool requeue = true)
        {
            lock (_lock)
            {
                var seg = _segments.FirstOrDefault(s => s.Id == segmentId);
                if (seg != null)
                {
                    if (seg.State == SegmentState.Completed)
                    {
                        // Terminal state: Completed segments cannot be set to Pending or Failed
                        return false;
                    }

                    if (requeue)
                    {
                        seg.State = SegmentState.Pending;
                    }
                    else
                    {
                        seg.State = SegmentState.Failed;
                    }
                    seg.AssignedWorkerId = null;
                    return true;
                }
                return false;
            }
        }

        public long GetTotalBytesDownloaded()
        {
            lock (_lock)
            {
                return _segments.Sum(s => Math.Min(s.TotalBytes, s.BytesDownloaded));
            }
        }

        public bool IsFullyCompleted()
        {
            lock (_lock)
            {
                return _segments.Count > 0 && _segments.All(s => s.IsCompleted);
            }
        }

        public List<SegmentRange> GetSegmentsSnapshot()
        {
            lock (_lock)
            {
                return _segments.Select(s => s.Clone()).OrderBy(s => s.Start).ToList();
            }
        }

        public bool ValidateCoverage()
        {
            lock (_lock)
            {
                if (_segments.Count == 0) return false;
                var sorted = _segments.OrderBy(s => s.Start).ToList();

                if (sorted[0].Start != 0) return false;

                for (int i = 0; i < sorted.Count - 1; i++)
                {
                    if (sorted[i].End + 1 != sorted[i + 1].Start)
                    {
                        return false; // Gap or overlap detected
                    }
                }

                if (sorted.Last().End != _totalBytes - 1) return false;

                return true;
            }
        }
    }
}
