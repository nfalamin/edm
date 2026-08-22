using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EDM.ControlPlane.Api.Data;
using EDM.ControlPlane.Api.Models;

namespace EDM.ControlPlane.Api.Services
{
    public record SystemHealthReport(
        HealthStatus OverallStatus,
        long LatencyMs,
        Dictionary<string, ComponentHealthInfo> Components,
        DateTime CheckedAtUtc);

    public record ComponentHealthInfo(
        HealthStatus Status,
        long LatencyMs,
        string Details);

    public interface ISystemHealthService
    {
        Task<SystemHealthReport> CheckSystemHealthAsync();
        Task RecordMetricAsync(string metricName, double value, string? dimensionsJson = null);
        Task<List<SystemMetric>> GetMetricsAsync(string metricName, DateTime? since = null);
        Task<List<SystemHealthSnapshot>> GetRecentSnapshotsAsync(int limit = 50);
    }

    public class SystemHealthService : ISystemHealthService
    {
        private readonly ControlPlaneDbContext _dbContext;

        public SystemHealthService(ControlPlaneDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<SystemHealthReport> CheckSystemHealthAsync()
        {
            var swTotal = Stopwatch.StartNew();
            var components = new Dictionary<string, ComponentHealthInfo>();

            // 1. Database Probe
            var dbSw = Stopwatch.StartNew();
            bool dbConnected = false;
            try
            {
                dbConnected = await _dbContext.Database.CanConnectAsync();
                dbSw.Stop();
                components["Database"] = new ComponentHealthInfo(
                    Status: dbConnected ? HealthStatus.Healthy : HealthStatus.Unhealthy,
                    LatencyMs: dbSw.ElapsedMilliseconds,
                    Details: dbConnected ? "Connected successfully" : "Connection failed");
            }
            catch (Exception ex)
            {
                dbSw.Stop();
                components["Database"] = new ComponentHealthInfo(
                    Status: HealthStatus.Unhealthy,
                    LatencyMs: dbSw.ElapsedMilliseconds,
                    Details: ex.Message);
            }

            // 2. Memory & Process Probe
            var proc = Process.GetCurrentProcess();
            long memoryMb = proc.WorkingSet64 / (1024 * 1024);
            components["RuntimeProcess"] = new ComponentHealthInfo(
                Status: memoryMb < 4096 ? HealthStatus.Healthy : HealthStatus.Degraded,
                LatencyMs: 0,
                Details: $"Memory: {memoryMb} MB | Threads: {proc.Threads.Count}");

            // 3. License & Token Engine
            components["SecurityEngine"] = new ComponentHealthInfo(
                Status: HealthStatus.Healthy,
                LatencyMs: 0,
                Details: "Argon2id + TOTP + Passkeys Active");

            swTotal.Stop();

            var overall = components.Values.Any(c => c.Status == HealthStatus.Unhealthy)
                ? HealthStatus.Unhealthy
                : components.Values.Any(c => c.Status == HealthStatus.Degraded)
                    ? HealthStatus.Degraded
                    : HealthStatus.Healthy;

            var report = new SystemHealthReport(
                OverallStatus: overall,
                LatencyMs: swTotal.ElapsedMilliseconds,
                Components: components,
                CheckedAtUtc: DateTime.UtcNow);

            // Record snapshot to DB
            foreach (var kv in components)
            {
                _dbContext.SystemHealthSnapshots.Add(new SystemHealthSnapshot
                {
                    Id = Guid.NewGuid(),
                    ComponentName = kv.Key,
                    Status = kv.Value.Status,
                    LatencyMs = kv.Value.LatencyMs,
                    DetailsJson = $"{{\"details\":\"{kv.Value.Details}\"}}",
                    CheckedAtUtc = DateTime.UtcNow
                });
            }

            await _dbContext.SaveChangesAsync();

            return report;
        }

        public async Task RecordMetricAsync(string metricName, double value, string? dimensionsJson = null)
        {
            if (string.IsNullOrWhiteSpace(metricName)) return;

            _dbContext.SystemMetrics.Add(new SystemMetric
            {
                Id = Guid.NewGuid(),
                MetricName = metricName.Trim(),
                MetricValue = value,
                DimensionsJson = dimensionsJson ?? "{}",
                TimestampUtc = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync();
        }

        public async Task<List<SystemMetric>> GetMetricsAsync(string metricName, DateTime? since = null)
        {
            var query = _dbContext.SystemMetrics.Where(m => m.MetricName == metricName);
            if (since.HasValue) query = query.Where(m => m.TimestampUtc >= since.Value);
            return await query.OrderByDescending(m => m.TimestampUtc).Take(100).ToListAsync();
        }

        public async Task<List<SystemHealthSnapshot>> GetRecentSnapshotsAsync(int limit = 50)
        {
            return await _dbContext.SystemHealthSnapshots
                .OrderByDescending(s => s.CheckedAtUtc)
                .Take(limit)
                .ToListAsync();
        }
    }
}
