using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Bee.Hub.EfCore.Entities;

namespace Bee.Hub.EfCore.Stores
{
    internal class EfCoreOutboxStore : IOutboxStore
    {
        private readonly BeeHubDbContext _db;

        public EfCoreOutboxStore(BeeHubDbContext db)
        {
            _db = db;
        }

            public void AddToContext(OutboxMessage msg)
            {
                _db.Outbox.Add(msg);
            }

        public async Task AddAsync(OutboxMessage msg, CancellationToken cancellationToken = default)
        {
            _db.Outbox.Add(msg);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<IList<OutboxMessage>> GetPendingBatchAsync(int size, DateTime now, CancellationToken cancellationToken = default)
        {
            return await _db.Outbox
                .Where(o => o.Status == "Pending" && o.AvailableAt <= now)
                .OrderBy(o => o.AvailableAt)
                .Take(size)
                .ToListAsync(cancellationToken);
        }

        public async Task MarkSentAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var e = await _db.Outbox.FindAsync(new object[] { id }, cancellationToken);
            if (e != null)
            {
                e.Status = "Sent";
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task MarkDeadLetterAsync(Guid id, string reason, CancellationToken cancellationToken = default)
        {
            var e = await _db.Outbox.FindAsync(new object[] { id }, cancellationToken);
            if (e != null)
            {
                e.Status = "DeadLetter";
                e.TransportMetadata = reason;
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task IncrementAttemptAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var e = await _db.Outbox.FindAsync(new object[] { id }, cancellationToken);
            if (e != null)
            {
                e.AttemptCount = e.AttemptCount + 1;
                e.LastAttemptAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task IncrementAttemptBatchAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
        {
            var idList = ids.ToList();
            if (!idList.Any()) return;

            var items = await _db.Outbox.Where(o => idList.Contains(o.Id)).ToListAsync(cancellationToken);
            var now = DateTime.UtcNow;
            foreach (var it in items)
            {
                it.AttemptCount = it.AttemptCount + 1;
                it.LastAttemptAt = now;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task MarkDeadLetterBatchAsync(IEnumerable<Guid> ids, string reason, CancellationToken cancellationToken = default)
        {
            var idList = ids.ToList();
            if (!idList.Any()) return;

            var items = await _db.Outbox.Where(o => idList.Contains(o.Id)).ToListAsync(cancellationToken);
            foreach (var it in items)
            {
                it.Status = "DeadLetter";
                it.TransportMetadata = reason;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task MarkDeadLetterBatchAsync(System.Collections.Generic.IEnumerable<System.ValueTuple<Guid, string>> items, CancellationToken cancellationToken = default)
        {
            var list = items.ToList();
            if (!list.Any()) return;

            var ids = list.Select(x => x.Item1).ToList();
            var entities = await _db.Outbox.Where(o => ids.Contains(o.Id)).ToListAsync(cancellationToken);
            var map = list.ToDictionary(x => x.Item1, x => x.Item2);
            foreach (var e in entities)
            {
                if (map.TryGetValue(e.Id, out var md))
                {
                    e.Status = "DeadLetter";
                    e.TransportMetadata = md;
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task MarkSentBatchAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
        {
            var idList = ids.ToList();
            if (!idList.Any()) return;

            var items = await _db.Outbox.Where(o => idList.Contains(o.Id)).ToListAsync(cancellationToken);
            foreach (var it in items)
            {
                it.Status = "Sent";
            }

            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
