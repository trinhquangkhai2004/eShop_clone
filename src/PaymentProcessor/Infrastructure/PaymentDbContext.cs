using eShop.PaymentProcessor.Domain;
using eShop.PaymentProcessor.Infrastructure.EntityConfigurations;

namespace eShop.PaymentProcessor.Infrastructure;

/// <remarks>
/// Add migrations using the following command inside the 'PaymentProcessor' project directory:
///
/// dotnet ef migrations add [migration-name]
/// </remarks>
public class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    public DbSet<PaymentTransaction> PaymentTransactions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("payment");
        modelBuilder.ApplyConfiguration(new PaymentTransactionEntityTypeConfiguration());
        modelBuilder.UseIntegrationEventLogs();
    }
}
