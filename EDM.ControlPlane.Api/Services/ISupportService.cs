using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EDM.ControlPlane.Api.Data;
using EDM.ControlPlane.Api.Models;

namespace EDM.ControlPlane.Api.Services
{
    public record CreateTicketRequest(
        Guid? UserId,
        string CustomerEmail,
        string CustomerName,
        string Subject,
        TicketCategory Category,
        TicketPriority Priority,
        string InitialMessage);

    public interface ISupportService
    {
        Task<SupportTicket> CreateTicketAsync(CreateTicketRequest request);
        Task<(int TotalCount, List<SupportTicket> Tickets)> GetTicketsAsync(int page = 1, int pageSize = 50, TicketStatus? status = null, TicketPriority? priority = null, string? search = null);
        Task<SupportTicket?> GetTicketWithMessagesAsync(Guid ticketId);
        Task<SupportMessage> AddMessageAsync(Guid ticketId, Guid? senderId, string senderName, MessageSenderType senderType, string messageContent, string? attachmentsJson = null);
        Task<bool> UpdateTicketStatusAsync(Guid ticketId, TicketStatus status, Guid? adminActorId = null);
        Task<bool> AssignTicketAsync(Guid ticketId, Guid assignedAdminId, Guid? adminActorId = null);
    }

    public class SupportService : ISupportService
    {
        private readonly ControlPlaneDbContext _dbContext;
        private readonly IAuditLoggingService _auditLogger;

        public SupportService(ControlPlaneDbContext dbContext, IAuditLoggingService auditLogger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        }

        public async Task<SupportTicket> CreateTicketAsync(CreateTicketRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.CustomerEmail) || string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.InitialMessage))
            {
                throw new ArgumentException("Email, subject, and initial message are required.");
            }

            int count = await _dbContext.SupportTickets.CountAsync();
            string ticketNumber = $"EDM-TK-{1000 + count + 1}";

            var ticket = new SupportTicket
            {
                Id = Guid.NewGuid(),
                TicketNumber = ticketNumber,
                UserId = request.UserId,
                CustomerEmail = request.CustomerEmail.Trim().ToLowerInvariant(),
                CustomerName = request.CustomerName.Trim(),
                Subject = request.Subject.Trim(),
                Category = request.Category,
                Priority = request.Priority,
                Status = TicketStatus.Open,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            var message = new SupportMessage
            {
                Id = Guid.NewGuid(),
                TicketId = ticket.Id,
                SenderId = request.UserId,
                SenderName = request.CustomerName.Trim(),
                SenderType = MessageSenderType.Customer,
                MessageContent = request.InitialMessage.Trim(),
                CreatedAtUtc = DateTime.UtcNow
            };

            ticket.Messages.Add(message);
            _dbContext.SupportTickets.Add(ticket);
            await _dbContext.SaveChangesAsync();

            await _auditLogger.LogActionAsync(
                actorId: request.UserId,
                actorUsername: request.CustomerEmail,
                action: "SUPPORT_TICKET_CREATED",
                targetEntity: "SupportTicket",
                targetId: ticket.Id.ToString(),
                detailsJson: $"{{\"ticketNumber\":\"{ticketNumber}\",\"subject\":\"{ticket.Subject}\"}}",
                correlationId: Guid.NewGuid().ToString("N"));

            return ticket;
        }

        public async Task<(int TotalCount, List<SupportTicket> Tickets)> GetTicketsAsync(int page = 1, int pageSize = 50, TicketStatus? status = null, TicketPriority? priority = null, string? search = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 50;

            var query = _dbContext.SupportTickets
                .Include(t => t.AssignedAdmin)
                .AsQueryable();

            if (status.HasValue) query = query.Where(t => t.Status == status.Value);
            if (priority.HasValue) query = query.Where(t => t.Priority == priority.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.Trim().ToLowerInvariant();
                query = query.Where(t => t.TicketNumber.ToLower().Contains(s) || t.Subject.ToLower().Contains(s) || t.CustomerEmail.ToLower().Contains(s));
            }

            int total = await query.CountAsync();
            var list = await query
                .OrderByDescending(t => t.UpdatedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (total, list);
        }

        public async Task<SupportTicket?> GetTicketWithMessagesAsync(Guid ticketId)
        {
            return await _dbContext.SupportTickets
                .Include(t => t.AssignedAdmin)
                .Include(t => t.Messages.OrderBy(m => m.CreatedAtUtc))
                .FirstOrDefaultAsync(t => t.Id == ticketId);
        }

        public async Task<SupportMessage> AddMessageAsync(Guid ticketId, Guid? senderId, string senderName, MessageSenderType senderType, string messageContent, string? attachmentsJson = null)
        {
            var ticket = await _dbContext.SupportTickets.FindAsync(ticketId);
            if (ticket == null) throw new InvalidOperationException($"Ticket '{ticketId}' not found.");

            var message = new SupportMessage
            {
                Id = Guid.NewGuid(),
                TicketId = ticketId,
                SenderId = senderId,
                SenderName = senderName.Trim(),
                SenderType = senderType,
                MessageContent = messageContent.Trim(),
                AttachmentsJson = attachmentsJson ?? "[]",
                CreatedAtUtc = DateTime.UtcNow
            };

            ticket.UpdatedAtUtc = DateTime.UtcNow;
            if (senderType == MessageSenderType.Admin && ticket.Status == TicketStatus.Open)
            {
                ticket.Status = TicketStatus.InProgress;
            }

            _dbContext.SupportMessages.Add(message);
            await _dbContext.SaveChangesAsync();

            await _auditLogger.LogActionAsync(
                actorId: senderId,
                actorUsername: senderName,
                action: "SUPPORT_MESSAGE_POSTED",
                targetEntity: "SupportTicket",
                targetId: ticketId.ToString(),
                detailsJson: $"{{\"senderType\":\"{senderType}\"}}",
                correlationId: Guid.NewGuid().ToString("N"));

            return message;
        }

        public async Task<bool> UpdateTicketStatusAsync(Guid ticketId, TicketStatus status, Guid? adminActorId = null)
        {
            var ticket = await _dbContext.SupportTickets.FindAsync(ticketId);
            if (ticket == null) return false;

            ticket.Status = status;
            ticket.UpdatedAtUtc = DateTime.UtcNow;
            if (status == TicketStatus.Resolved || status == TicketStatus.Closed)
            {
                ticket.ResolvedAtUtc = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();

            await _auditLogger.LogActionAsync(
                actorId: adminActorId,
                actorUsername: "ADMIN",
                action: "SUPPORT_TICKET_STATUS_CHANGED",
                targetEntity: "SupportTicket",
                targetId: ticketId.ToString(),
                detailsJson: $"{{\"status\":\"{status}\"}}",
                correlationId: Guid.NewGuid().ToString("N"));

            return true;
        }

        public async Task<bool> AssignTicketAsync(Guid ticketId, Guid assignedAdminId, Guid? adminActorId = null)
        {
            var ticket = await _dbContext.SupportTickets.FindAsync(ticketId);
            if (ticket == null) return false;

            var admin = await _dbContext.Users.FindAsync(assignedAdminId);
            if (admin == null) return false;

            ticket.AssignedAdminId = assignedAdminId;
            ticket.UpdatedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            await _auditLogger.LogActionAsync(
                actorId: adminActorId,
                actorUsername: "ADMIN",
                action: "SUPPORT_TICKET_ASSIGNED",
                targetEntity: "SupportTicket",
                targetId: ticketId.ToString(),
                detailsJson: $"{{\"assignedAdmin\":\"{admin.Username}\"}}",
                correlationId: Guid.NewGuid().ToString("N"));

            return true;
        }
    }
}
