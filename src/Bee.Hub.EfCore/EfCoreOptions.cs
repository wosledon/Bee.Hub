namespace Bee.Hub.EfCore
{
    public class EfCoreOptions
    {
        public string? OutboxTableName { get; set; }
        public int DispatchBatchSize { get; set; } = 50;
        public int DispatchIntervalMs { get; set; } = 1000;
        public int MaxRetryAttempts { get; set; } = 5;
    }
}