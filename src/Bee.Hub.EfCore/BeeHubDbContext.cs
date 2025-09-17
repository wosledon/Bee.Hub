using Microsoft.EntityFrameworkCore;
using Bee.Hub.EfCore.Entities;

namespace Bee.Hub.EfCore
{
    public class BeeHubDbContext : DbContext
    {
        public DbSet<OutboxMessage> Outbox { get; set; } = null!;
        public DbSet<InboxMessage> Inbox { get; set; } = null!;

        public BeeHubDbContext(DbContextOptions<BeeHubDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OutboxMessage>().HasKey(x => x.Id);
            modelBuilder.Entity<OutboxMessage>().HasIndex(x => new { x.Status, x.AvailableAt });

            modelBuilder.Entity<InboxMessage>().HasKey(x => x.Id);
            modelBuilder.Entity<InboxMessage>().HasIndex(x => x.MessageId).IsUnique(false);

            base.OnModelCreating(modelBuilder);
        }
    }
}