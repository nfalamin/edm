using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EDM.ControlPlane.Api.Middleware;
using EDM.ControlPlane.Api.Models;
using EDM.ControlPlane.Api.Services;

namespace EDM.ControlPlane.Api.Controllers
{
    public record CreateTicketDto(
        string CustomerEmail,
        string CustomerName,
        string Subject,
        TicketCategory Category,
        TicketPriority Priority,
        string Message);

    public record ReplyTicketDto(string MessageContent, string? AttachmentsJson);
    public record UpdateTicketStatusDto(TicketStatus Status);
    public record AssignTicketDto(Guid AssignedAdminId);

    [ApiController]
    [Route("api/v1/support")]
    public class SupportController : ControllerBase
    {
        private readonly ISupportService _supportService;

        public SupportController(ISupportService supportService)
        {
            _supportService = supportService ?? throw new ArgumentNullException(nameof(supportService));
        }

        // Public / Authenticated user ticket creation
        [HttpPost("tickets")]
        public async Task<IActionResult> CreateTicketAsync([FromBody] CreateTicketDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.CustomerEmail) || string.IsNullOrWhiteSpace(request.Subject))
            {
                return BadRequest(new { error = "INVALID_PAYLOAD", message = "Email and subject are required." });
            }

            Guid? userId = null;
            if (User?.Identity != null && User.Identity.IsAuthenticated)
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
                if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var uId))
                {
                    userId = uId;
                }
            }

            var ticket = await _supportService.CreateTicketAsync(new CreateTicketRequest(
                UserId: userId,
                CustomerEmail: request.CustomerEmail,
                CustomerName: request.CustomerName,
                Subject: request.Subject,
                Category: request.Category,
                Priority: request.Priority,
                InitialMessage: request.Message));

            return Ok(new
            {
                success = true,
                ticketId = ticket.Id,
                ticketNumber = ticket.TicketNumber,
                status = ticket.Status.ToString()
            });
        }

        [Authorize]
        [RequirePermission(Permissions.SupportManage)]
        [HttpGet("tickets")]
        public async Task<IActionResult> GetTicketsAsync(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] TicketStatus? status = null,
            [FromQuery] TicketPriority? priority = null,
            [FromQuery] string? search = null)
        {
            var (totalCount, tickets) = await _supportService.GetTicketsAsync(page, pageSize, status, priority, search);
            return Ok(new
            {
                totalCount,
                page,
                pageSize,
                tickets = tickets.ConvertAll(t => new
                {
                    t.Id,
                    t.TicketNumber,
                    t.CustomerEmail,
                    t.CustomerName,
                    t.Subject,
                    Category = t.Category.ToString(),
                    Priority = t.Priority.ToString(),
                    Status = t.Status.ToString(),
                    AssignedAdmin = t.AssignedAdmin?.Username,
                    t.CreatedAtUtc,
                    t.UpdatedAtUtc,
                    t.ResolvedAtUtc
                })
            });
        }

        [Authorize]
        [RequirePermission(Permissions.SupportManage)]
        [HttpGet("tickets/{id}")]
        public async Task<IActionResult> GetTicketByIdAsync(Guid id)
        {
            var ticket = await _supportService.GetTicketWithMessagesAsync(id);
            if (ticket == null) return NotFound(new { error = "NOT_FOUND", message = "Support ticket not found." });

            return Ok(new
            {
                ticket.Id,
                ticket.TicketNumber,
                ticket.CustomerEmail,
                ticket.CustomerName,
                ticket.Subject,
                Category = ticket.Category.ToString(),
                Priority = ticket.Priority.ToString(),
                Status = ticket.Status.ToString(),
                AssignedAdmin = ticket.AssignedAdmin?.Username,
                ticket.CreatedAtUtc,
                ticket.UpdatedAtUtc,
                ticket.ResolvedAtUtc,
                messages = ticket.Messages.Select(m => new
                {
                    m.Id,
                    m.SenderName,
                    SenderType = m.SenderType.ToString(),
                    m.MessageContent,
                    m.AttachmentsJson,
                    m.CreatedAtUtc
                })
            });
        }

        [Authorize]
        [RequirePermission(Permissions.SupportManage)]
        [HttpPost("tickets/{id}/reply")]
        public async Task<IActionResult> ReplyToTicketAsync(Guid id, [FromBody] ReplyTicketDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.MessageContent))
            {
                return BadRequest(new { error = "INVALID_PAYLOAD", message = "Message content is required." });
            }

            var adminName = User.Identity?.Name ?? "SUPPORT_ADMIN";
            var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            Guid? adminId = adminIdClaim != null && Guid.TryParse(adminIdClaim.Value, out var aId) ? aId : null;

            var msg = await _supportService.AddMessageAsync(
                ticketId: id,
                senderId: adminId,
                senderName: adminName,
                senderType: MessageSenderType.Admin,
                messageContent: request.MessageContent,
                attachmentsJson: request.AttachmentsJson);

            return Ok(new { success = true, messageId = msg.Id });
        }

        [Authorize]
        [RequirePermission(Permissions.SupportManage)]
        [HttpPut("tickets/{id}/status")]
        public async Task<IActionResult> UpdateTicketStatusAsync(Guid id, [FromBody] UpdateTicketStatusDto request)
        {
            var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            Guid? adminId = adminIdClaim != null && Guid.TryParse(adminIdClaim.Value, out var aId) ? aId : null;

            bool success = await _supportService.UpdateTicketStatusAsync(id, request.Status, adminId);
            if (!success) return NotFound(new { error = "NOT_FOUND", message = "Ticket not found." });

            return Ok(new { success = true, message = $"Status updated to {request.Status}." });
        }

        [Authorize]
        [RequirePermission(Permissions.SupportManage)]
        [HttpPut("tickets/{id}/assign")]
        public async Task<IActionResult> AssignTicketAsync(Guid id, [FromBody] AssignTicketDto request)
        {
            var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            Guid? adminId = adminIdClaim != null && Guid.TryParse(adminIdClaim.Value, out var aId) ? aId : null;

            bool success = await _supportService.AssignTicketAsync(id, request.AssignedAdminId, adminId);
            if (!success) return NotFound(new { error = "NOT_FOUND", message = "Ticket or Admin not found." });

            return Ok(new { success = true, message = "Ticket assigned successfully." });
        }
    }
}
