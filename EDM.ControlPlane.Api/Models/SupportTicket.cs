using System;
using System.Collections.Generic;

namespace EDM.ControlPlane.Api.Models
{
    public enum TicketCategory
    {
        Billing,
        Technical,
        BugReport,
        FeatureRequest,
        Account,
        General
    }

    public enum TicketPriority
    {
        Low,
        Medium,
        High,
        Urgent
    }

    public enum TicketStatus
    {
        Open,
        PendingCustomer,
        InProgress,
        Resolved,
        Closed
    }

    public enum MessageSenderType
    {
        Customer,
        Admin,
        System
    }

    public class SupportTicket
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string TicketNumber { get; set; } = string.Empty; // e.g. "EDM-TK-1001"
        public Guid? UserId { get; set; }
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public TicketCategory Category { get; set; } = TicketCategory.General;
        public TicketPriority Priority { get; set; } = TicketPriority.Medium;
        public TicketStatus Status { get; set; } = TicketStatus.Open;
        public Guid? AssignedAdminId { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? ResolvedAtUtc { get; set; }

        // Navigation
        public User? User { get; set; }
        public User? AssignedAdmin { get; set; }
        public ICollection<SupportMessage> Messages { get; set; } = new List<SupportMessage>();
    }

    public class SupportMessage
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TicketId { get; set; }
        public Guid? SenderId { get; set; } // Admin/User ID if authenticated
        public string SenderName { get; set; } = string.Empty;
        public MessageSenderType SenderType { get; set; } = MessageSenderType.Customer;
        public string MessageContent { get; set; } = string.Empty;
        public string AttachmentsJson { get; set; } = "[]";
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        // Navigation
        public SupportTicket? Ticket { get; set; }
    }
}
