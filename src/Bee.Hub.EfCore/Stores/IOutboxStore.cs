using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bee.Hub.EfCore.Entities;

namespace Bee.Hub.EfCore.Stores
{
    public interface IOutboxStore
    {
        // Add message directly to the DbContext (no SaveChanges) - safe to call from SaveChanges interceptor
        void AddToContext(OutboxMessage msg);

        Task AddAsync(OutboxMessage msg, CancellationToken cancellationToken = default);
        Task<IList<OutboxMessage>> GetPendingBatchAsync(int size, DateTime now, CancellationToken cancellationToken = default);
        Task MarkSentAsync(Guid id, CancellationToken cancellationToken = default);
        Task MarkDeadLetterAsync(Guid id, string reason, CancellationToken cancellationToken = default);
        // Increment attempt count and set LastAttemptAt
        Task IncrementAttemptAsync(Guid id, CancellationToken cancellationToken = default);
        Task MarkDeadLetterBatchAsync(IEnumerable<Guid> ids, string reason, CancellationToken cancellationToken = default);
        Task MarkSentBatchAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    }
}
