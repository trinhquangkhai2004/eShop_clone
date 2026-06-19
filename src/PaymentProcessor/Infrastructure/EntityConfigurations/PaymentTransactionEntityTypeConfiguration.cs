using eShop.PaymentProcessor.Domain;

namespace eShop.PaymentProcessor.Infrastructure.EntityConfigurations;

class PaymentTransactionEntityTypeConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable("PaymentTransactions");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .UseIdentityByDefaultColumn();

        builder.Property(t => t.OrderId)
            .IsRequired();

        builder.Property(t => t.UserId)
            .HasMaxLength(256);

        builder.Property(t => t.Amount)
            .HasPrecision(18, 2);

        builder.Property(t => t.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(t => t.PaymentMethod)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(t => t.IdempotencyKey)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(t => t.GatewayTransactionId)
            .HasMaxLength(256);

        builder.Property(t => t.FailureReason)
            .HasMaxLength(512);

        builder.Property(t => t.ReconciliationAttempts)
            .IsRequired();

        builder.Property(t => t.LastReconciledAt);

        builder.Property(t => t.OutboxTransactionId);

        builder.Property(t => t.ResultEventPublished)
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .IsRequired();

        builder.Property(t => t.UpdatedAt)
            .IsRequired();

        builder.HasIndex(t => t.OrderId)
            .IsUnique();

        builder.HasIndex(t => t.IdempotencyKey)
            .IsUnique();

        builder.HasIndex(t => new { t.Status, t.UpdatedAt });

        builder.HasIndex(t => new { t.Status, t.ResultEventPublished });

        builder.HasIndex(t => t.OutboxTransactionId);
    }
}
