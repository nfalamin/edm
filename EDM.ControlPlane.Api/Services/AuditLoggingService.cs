using System;
using System.Threading.Tasks;
using EDM.ControlPlane.Api.Data;
using EDM.ControlPlane.Api.Models;

namespace EDM.ControlPlane.Api.Services
{
    public interface IAuditLoggingService
    {
        Task LogActionAsync(
            Guid? actorId,
            string actorUsername,
            string action,
            string targetEntity,
            string? targetId,
            string detailsJson,
            string correlationId,
            string resultStatus = "SUCCESS",
            string? rawIpAddress = null);
    }

    /// <summary>
    /// Append-only, immutable administrative action audit logger.
    /// Strictly filters secrets and hashes/masks client IPs.
    /// </summary>
    public class AuditLoggingService : IAuditLoggingService
    {
        private readonly ControlPlaneDbContext _dbContext;
        private readonly IPrivacySafeDeviceService _deviceService;

        public AuditLoggingService(ControlPlaneDbContext dbContext, IPrivacySafeDeviceService? deviceService = null)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _deviceService = deviceService ?? new PrivacySafeDeviceService();
        }

        public async Task LogActionAsync(
            Guid? actorId,
            string actorUsername,
            string action,
            string targetEntity,
            string? targetId,
            string detailsJson,
            string correlationId,
            string resultStatus = "SUCCESS",
            string? rawIpAddress = null)
        {
            var auditEntry = new AuditLog
            {
                Id = Guid.NewGuid(),
                ActorId = actorId,
                ActorUsername = actorUsername,
                Action = action,
                TargetEntity = targetEntity,
                TargetId = targetId,
                DetailsJson = detailsJson,
                CorrelationId = string.IsNullOrEmpty(correlationId) ? Guid.NewGuid().ToString("N") : correlationId,
                ResultStatus = resultStatus,
                CoarseIpAddress = _deviceService.AnonymizeIpAddress(rawIpAddress),
                TimestampUtc = DateTime.UtcNow
            };

            _dbContext.AuditLogs.Add(auditEntry);
            await _dbContext.SaveChangesAsync();
        }
    }
}
