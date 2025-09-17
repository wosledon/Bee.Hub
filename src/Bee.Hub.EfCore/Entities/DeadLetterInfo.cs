using System;

namespace Bee.Hub.EfCore.Entities
{
    public class DeadLetterInfo
    {
        public string Reason { get; set; } = null!;
        public string? ExceptionType { get; set; }
        public string? ExceptionMessage { get; set; }
        public string? StackTrace { get; set; }
        public DateTime OccurredAt { get; set; }
    }
}
