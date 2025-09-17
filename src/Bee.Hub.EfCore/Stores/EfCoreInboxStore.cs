using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Bee.Hub.EfCore.Entities;

namespace Bee.Hub.EfCore.Stores
{
    internal class EfCoreInboxStore : IInboxStore
    {
        private readonly BeeHubDbContext _db;

        public EfCoreInboxStore(BeeHubDbContext db)
        {
            _db = db;
        }

        public async Task<bool> TryMarkProcessedAsync(string messageId, string handler)
        {
            // Basic implementation: insert a record if not exist
            var exists = await _db.Inbox.AnyAsync(i => i.MessageId == messageId);
            if (exists) return false;

            _db.Inbox.Add(new InboxMessage
            {
                Id = System.Guid.NewGuid(),
                MessageId = messageId,
                MessageType = string.Empty,
                ReceivedAt = System.DateTime.UtcNow,
                ProcessedAt = System.DateTime.UtcNow,
                Handler = handler,
                Status = "Processed"
            });

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> IsProcessedAsync(string messageId)
        {
            return await _db.Inbox.AnyAsync(i => i.MessageId == messageId);
        }
    }
}
