using CandidateService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CandidateService.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<Operation> Operations { get; set; }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Operation>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasMaxLength(100);
                entity.Property(e => e.Amount).HasPrecision(18, 2);
                entity.Property(e => e.Currency).HasMaxLength(3);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.ProviderPaymentId).HasMaxLength(100);

                entity.OwnsMany(e => e.Events, events =>
                {
                    events.WithOwner().HasForeignKey("OperationId");
                    events.Property(e => e.EventId).ValueGeneratedNever();
                    events.Property(e => e.Type).HasMaxLength(50);
                    events.Property(e => e.FromStatus).HasMaxLength(50);
                    events.Property(e => e.ToStatus).HasMaxLength(50);
                    events.Property(e => e.Message).HasMaxLength(500);
                    events.HasKey("OperationId", "EventId");
                });
            });
        }
    }
}
