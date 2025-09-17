using System;

namespace Bee.Hub.EfCore.Entities
{
    public class OutboxMessage
    {
        public Guid Id { get; set; }
        public string MessageType { get; set; } = null!;
        public byte[] Payload { get; set; } = null!; // serialized bytes
        public string Headers { get; set; } = null!; // JSON
        public DateTime CreatedAt { get; set; }
        public DateTime AvailableAt { get; set; }
        public int AttemptCount { get; set; }
        public DateTime? LastAttemptAt { get; set; }
        public string Status { get; set; } = "Pending";
        public string? TransportMetadata { get; set; }
    }
}