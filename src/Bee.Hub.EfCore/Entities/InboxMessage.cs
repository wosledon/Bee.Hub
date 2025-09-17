using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bee.Hub.EfCore.Entities
{
    [Table("bee_hub_inbox_messages")]
    public class InboxMessage
    {
        public Guid Id { get; set; }
        public string MessageId { get; set; } = null!; // bh-message-id
        public string MessageType { get; set; } = null!;
        public DateTime ReceivedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public string? Handler { get; set; }
        public string Status { get; set; } = "Received";
    }
}