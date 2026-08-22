using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace EDM.Services
{
    public class DomainSpeedMetric
    {
        public string Domain { get; set; } = string.Empty;
        public long TotalBytesDownloaded { get; set; }
        public double MaxSpeedBytesPerSec { get; set; }
        public double AvgSpeedBytesPerSec { get; set; }
        public int SampleCount { get; set; }
    }

    public class SpeedHeatmapCell
    {
        public DayOfWeek DayOfWeek { get; set; }
        public int HourOfDay { get; set; } // 0 - 23
        public double AverageSpeedMbps { get; set; }
        public long TotalBytesTransferred { get; set; }
    }

    public class AnalyticsOverviewReport
    {
        public long TotalBytesDownloadedAllTime { get; set; }
        public long TodayBytesDownloaded { get; set; }
        public long ThisMonthBytesDownloaded { get; set; }
        public double PeakRecordedSpeedMbps { get; set; }
        public double CurrentAverageSpeedMbps { get; set; }
        public List<DomainSpeedMetric> TopFastestDomains { get; set; } = new();
        public List<SpeedHeatmapCell> WeeklySpeedHeatmap { get; set; } = new();
        public string IspReliabilityGrade { get; set; } = "A+";
    }

    public class DownloadAnalyticsEngine
    {
        private readonly ConcurrentDictionary<string, DomainSpeedMetric> _domainMetrics = new(StringComparer.OrdinalIgnoreCase);
        private readonly double[,] _hourlySpeedAccumulator = new double[7, 24]; // Day x Hour
        private readonly long[,] _hourlyByteAccumulator = new long[7, 24];
        private readonly int[,] _hourlySampleCounts = new int[7, 24];
        private long _totalAllTimeBytes;
        private double _peakSpeedBytesPerSec;
        private readonly object _lock = new();

        public void RecordDownloadSample(string url, long bytesIncrement, double currentSpeedBytesPerSec)
        {
            if (bytesIncrement < 0) bytesIncrement = 0;
            if (currentSpeedBytesPerSec < 0) currentSpeedBytesPerSec = 0;

            var domain = ExtractDomain(url);
            var now = DateTime.Now;
            int day = (int)now.DayOfWeek;
            int hour = now.Hour;

            lock (_lock)
            {
                _totalAllTimeBytes += bytesIncrement;
                if (currentSpeedBytesPerSec > _peakSpeedBytesPerSec)
                {
                    _peakSpeedBytesPerSec = currentSpeedBytesPerSec;
                }

                // Accumulate hourly heatmap
                _hourlyByteAccumulator[day, hour] += bytesIncrement;
                _hourlySpeedAccumulator[day, hour] += currentSpeedBytesPerSec;
                _hourlySampleCounts[day, hour]++;
            }

            // Update domain metrics
            _domainMetrics.AddOrUpdate(domain,
                addValueFactory: d => new DomainSpeedMetric
                {
                    Domain = d,
                    TotalBytesDownloaded = bytesIncrement,
                    MaxSpeedBytesPerSec = currentSpeedBytesPerSec,
                    AvgSpeedBytesPerSec = currentSpeedBytesPerSec,
                    SampleCount = 1
                },
                updateValueFactory: (d, existing) =>
                {
                    existing.TotalBytesDownloaded += bytesIncrement;
                    if (currentSpeedBytesPerSec > existing.MaxSpeedBytesPerSec)
                    {
                        existing.MaxSpeedBytesPerSec = currentSpeedBytesPerSec;
                    }
                    existing.SampleCount++;
                    existing.AvgSpeedBytesPerSec = ((existing.AvgSpeedBytesPerSec * (existing.SampleCount - 1)) + currentSpeedBytesPerSec) / existing.SampleCount;
                    return existing;
                });
        }

        public AnalyticsOverviewReport GenerateOverviewReport()
        {
            var report = new AnalyticsOverviewReport();

            lock (_lock)
            {
                report.TotalBytesDownloadedAllTime = _totalAllTimeBytes;
                report.TodayBytesDownloaded = GetTodayBytes();
                report.ThisMonthBytesDownloaded = _totalAllTimeBytes; // baseline
                report.PeakRecordedSpeedMbps = (_peakSpeedBytesPerSec * 8) / 1_000_000.0;

                // Build 7x24 heatmap
                for (int d = 0; d < 7; d++)
                {
                    for (int h = 0; h < 24; h++)
                    {
                        int samples = _hourlySampleCounts[d, h];
                        double avgSpeed = samples > 0 ? (_hourlySpeedAccumulator[d, h] / samples) : 0.0;
                        double avgSpeedMbps = (avgSpeed * 8) / 1_000_000.0;

                        report.WeeklySpeedHeatmap.Add(new SpeedHeatmapCell
                        {
                            DayOfWeek = (DayOfWeek)d,
                            HourOfDay = h,
                            AverageSpeedMbps = Math.Round(avgSpeedMbps, 2),
                            TotalBytesTransferred = _hourlyByteAccumulator[d, h]
                        });
                    }
                }
            }

            // Rank fastest domains
            report.TopFastestDomains = _domainMetrics.Values
                .OrderByDescending(m => m.AvgSpeedBytesPerSec)
                .Take(10)
                .ToList();

            if (report.TopFastestDomains.Count > 0)
            {
                report.CurrentAverageSpeedMbps = Math.Round((report.TopFastestDomains.Average(d => d.AvgSpeedBytesPerSec) * 8) / 1_000_000.0, 2);
            }

            report.IspReliabilityGrade = CalculateIspGrade(report.PeakRecordedSpeedMbps);

            return report;
        }

        public DomainSpeedMetric? GetDomainMetric(string domainOrUrl)
        {
            var domain = ExtractDomain(domainOrUrl);
            _domainMetrics.TryGetValue(domain, out var metric);
            return metric;
        }

        private long GetTodayBytes()
        {
            int today = (int)DateTime.Now.DayOfWeek;
            long sum = 0;
            for (int h = 0; h < 24; h++)
            {
                sum += _hourlyByteAccumulator[today, h];
            }
            return sum;
        }

        private static string ExtractDomain(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return uri.Host.ToLowerInvariant();
            }
            return "unknown-domain";
        }

        private static string CalculateIspGrade(double peakMbps)
        {
            return peakMbps switch
            {
                >= 100.0 => "A+ (Gigabit / Ultra-Fast)",
                >= 50.0 => "A (High Speed)",
                >= 20.0 => "B+ (Standard Broadband)",
                >= 5.0 => "B (Fair)",
                _ => "C (Throttled / High Latency)"
            };
        }
    }
}
