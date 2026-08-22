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
        private readonly long _splitAlignmentBytes;
        private int _nextSegmentId = 0;

        public long TotalBytes => _totalBytes;
        public IReadOnlyList<SegmentRange> Segments => GetSegmentsSnapshot();

        public SegmentScheduler(
            long totalBytes,
            long minSplitThresholdBytes = 2 * 1024 * 1024,
            long splitAlignmentBytes = 64 * 1024)
        {
            if (totalBytes <= 0) throw new ArgumentOutOfRangeException(nameof(totalBytes));
            _totalBytes = totalBytes;
            _minSplitThresholdBytes = Math.Max(1, minSplitThresholdBytes);
            _splitAlignmentBytes = Math.Max(1, splitAlignmentBytes);
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

        /// <summary>
        /// Initializes default segment layout using smart initial segment sizing based on total file size.
        /// </summary>
        public void InitializeDefault(int requestedSegmentCount)
        {
            int smartCount = requestedSegmentCount > 0 
                ? Math.Min(requestedSegmentCount, (int)Math.Max(1, _totalBytes))
                : CalculateSmartSegmentCount(_totalBytes, 8);
            lock (_lock)
            {
                _segments.Clear();
                long baseSize = _totalBytes / smartCount;
                long remainder = _totalBytes % smartCount;
                long offset = 0;

                for (int i = 0; i < smartCount; i++)
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

        /// <summary>
        /// Computes smart segment count based on file size to prevent excessive fragmentation.
        /// </summary>
        public static int CalculateSmartSegmentCount(long totalBytes, int maxRequested)
        {
            if (totalBytes <= 0) return 1;
            int maxAllowed = Math.Max(1, maxRequested);

            // Small files (< 1 MB): 1 segment
            if (totalBytes < 1 * 1024 * 1024) return 1;

            // Small-medium files (1 MB - 5 MB): at most 2 segments
            if (totalBytes < 5 * 1024 * 1024) return Math.Min(2, maxAllowed);

            // Medium files (5 MB - 50 MB): at most 4 segments
            if (totalBytes < 50 * 1024 * 1024) return Math.Min(4, maxAllowed);

            // Large files (50 MB - 500 MB): at most 8 segments
            if (totalBytes < 500 * 1024 * 1024) return Math.Min(8, maxAllowed);

            // Very large files (> 500 MB): up to requested max
            return Math.Min(32, maxAllowed);
        }

        private readonly ConcurrentDictionary<string, WorkerPerformanceInfo> _workerTelemetry = new();

        public class WorkerPerformanceInfo
        {
            public string WorkerId { get; set; } = string.Empty;
            public int CurrentSegmentId { get; set; }
            public long BytesDownloaded { get; set; }
            public double SpeedBps { get; set; }
            public DateTime LastActivity { get; set; } = DateTime.UtcNow;
            public bool IsStalled => (DateTime.UtcNow - LastActivity).TotalSeconds > 4.0;
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

        /// <summary>
        /// Assigns the next work item: first unassigned Pending segment, or dynamic split / work steal from the largest Downloading segment.
        /// </summary>
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

                // 2. Dynamic Work Stealing: Split the largest downloading segment with sufficient remaining bytes
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
                        if (_splitAlignmentBytes > 1 && half > _splitAlignmentBytes * 2)
                        {
                            half = (half / _splitAlignmentBytes) * _splitAlignmentBytes;
                        }
                        if (half < _minSplitThresholdBytes) half = _minSplitThresholdBytes;

                        long splitPoint = candidate.End - half;
                        if (splitPoint > currentPos)
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

        /// <summary>
        /// Detects stalled workers that have made no progress for longer than the stall threshold and reclaims their ranges.
        /// </summary>
        public List<int> ReclaimStalledSegments(TimeSpan stallThreshold)
        {
            var reclaimedIds = new List<int>();
            var now = DateTime.UtcNow;

            lock (_lock)
            {
                foreach (var seg in _segments.Where(s => s.State == SegmentState.Downloading))
                {
                    if (!string.IsNullOrEmpty(seg.AssignedWorkerId) &&
                        _workerTelemetry.TryGetValue(seg.AssignedWorkerId, out var info))
                    {
                        if (now - info.LastActivity > stallThreshold)
                        {
                            LoggingService.LogWarning($"[SegmentScheduler] Reclaiming stalled segment {seg.Id} from worker {seg.AssignedWorkerId} (inactive for {(now - info.LastActivity).TotalSeconds:F1}s).");
                            seg.State = SegmentState.Pending;
                            seg.AssignedWorkerId = null;
                            reclaimedIds.Add(seg.Id);
                        }
                    }
                }
            }

            return reclaimedIds;
        }

        /// <summary>
        /// Reclaims any downloading segment currently assigned to the given worker.
        /// </summary>
        public void ReclaimWorkerSegment(string workerId)
        {
            if (string.IsNullOrEmpty(workerId)) return;
            lock (_lock)
            {
                var seg = _segments.FirstOrDefault(s => s.AssignedWorkerId == workerId && s.State == SegmentState.Downloading);
                if (seg != null)
                {
                    seg.State = SegmentState.Pending;
                    seg.AssignedWorkerId = null;
                }
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

        /// <summary>
        /// Validates that all segments form a complete, continuous, non-overlapping partition of [0, TotalBytes - 1].
        /// </summary>
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
