using System;
using System.Collections.Generic;

namespace EDM.Models
{
    public enum VerificationState
    {
        Pending = 0,
        Verified = 1,
        VerificationUnavailable = 2,
        VerificationFailed = 3
    }

    public class SegmentVerificationResult
    {
        public int Index { get; set; }
        public long ExpectedStart { get; set; }
        public long ExpectedEnd { get; set; }
        public long ActualLength { get; set; }
        public bool Complete { get; set; }
        public string? Message { get; set; }
    }

    public class VerificationResult
    {
        public VerificationState State { get; set; } = VerificationState.Pending;
        public string? Message { get; set; }
        public string? Algorithm { get; set; }
        public string? ComputedHashHex { get; set; }
        public string? ExpectedHashHex { get; set; }
        public long? ExpectedSize { get; set; }
        public long ActualSize { get; set; }
        public List<SegmentVerificationResult>? SegmentResults { get; set; }
    }
}
