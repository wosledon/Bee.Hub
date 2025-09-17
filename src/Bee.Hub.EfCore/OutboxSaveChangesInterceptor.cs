using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Bee.Hub.Core;
using Bee.Hub.EfCore.Entities;

namespace Bee.Hub.EfCore
{
    public interface IDomainEventHolder
    {
        // Implementations MUST ensure this returns a non-null, mutable list (e.g. new List<object>())
        IList<object> DomainEvents { get; }
    }

    internal class OutboxSaveChangesInterceptor : SaveChangesInterceptor
    {
        private readonly IMessageSerializer _serializer;
        private readonly Stores.IOutboxStore _outboxStore;

        public OutboxSaveChangesInterceptor(IMessageSerializer serializer, Stores.IOutboxStore outboxStore)
        {
            _serializer = serializer;
            _outboxStore = outboxStore;
        }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            HandleOutbox(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            HandleOutbox(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private void HandleOutbox(DbContext? context)
        {
            if (context == null) return;

            var entries = context.ChangeTracker.Entries()
                .Where(e => e.Entity is IDomainEventHolder)
                .Select(e => e.Entity as IDomainEventHolder)
                .Where(h => h != null)
                .ToList()!;

            foreach (var holder in entries)
            {
                if (holder == null) continue;

                var domainEvents = holder.DomainEvents;
                if (domainEvents == null || domainEvents.Count == 0) continue;

                var events = domainEvents.ToList();

                foreach (var ev in events)
                {
                    var payload = _serializer.Serialize(ev);
                    var outbox = new OutboxMessage
                    {
                        Id = Guid.NewGuid(),
                        MessageType = ev.GetType().FullName ?? ev.GetType().Name,
                        Payload = payload,
                        Headers = "{}",
                        CreatedAt = DateTime.UtcNow,
                        AvailableAt = DateTime.UtcNow,
                        AttemptCount = 0,
                        Status = "Pending"
                    };

                    // Add to the current DbContext via the store (store will add to context without saving)
                    _outboxStore.AddToContext(outbox);
                }

                // Clear safely (we checked not null above)
                holder.DomainEvents.Clear();
            }
        }
    }
}
