using Microsoft.EntityFrameworkCore;

namespace Erp.Messaging.Outbox;

public static class OutboxModelBuilderExtensions
{
    /// <summary>Maps the outbox table into the module's schema. Call from the module's OnModelCreating.</summary>
    public static ModelBuilder AddOutbox(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("outbox_messages");
            builder.HasKey(message => message.Id);
            builder.Property(message => message.Type).HasMaxLength(500);
            builder.Property(message => message.Content).HasColumnType("jsonb");
            builder.HasIndex(message => message.OccurredAt).HasFilter("processed_at IS NULL");
        });
        return modelBuilder;
    }
}
