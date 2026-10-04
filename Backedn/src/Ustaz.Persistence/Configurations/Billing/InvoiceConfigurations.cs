using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Billing;

namespace Ustaz.Persistence.Configurations.Billing;

public sealed class InvoiceConfiguration : BaseEntityConfiguration<Invoice>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");

        builder.Property(invoice => invoice.Number).HasMaxLength(50).IsRequired();
        builder.Property(invoice => invoice.Status).HasConversion<int>();
        builder.Property(invoice => invoice.Currency).HasMaxLength(3).IsRequired();
        builder.Property(invoice => invoice.Description).HasMaxLength(DomainRules.ShortTextMaxLength);

        builder.Property(invoice => invoice.Subtotal).HasPrecision(18, 2);
        builder.Property(invoice => invoice.TaxAmount).HasPrecision(18, 2);
        builder.Property(invoice => invoice.Total).HasPrecision(18, 2);

        builder.ToTable(table => table.HasCheckConstraint("CK_Invoices_Total", "[Total] >= 0"));

        // Platform-wide invoice numbering for USTAZ SaaS billing.
        builder.HasIndex(invoice => invoice.Number)
            .IsUnique()
            .HasDatabaseName("UX_Invoices_Number");

        builder.HasIndex(invoice => new { invoice.TenantId, invoice.Status, invoice.IssuedAt })
            .HasDatabaseName("IX_Invoices_TenantId_Status_IssuedAt");

        builder.HasIndex(invoice => invoice.TenantSubscriptionId)
            .HasDatabaseName("IX_Invoices_SubscriptionId");

        builder.HasOne(invoice => invoice.Tenant)
            .WithMany()
            .HasForeignKey(invoice => invoice.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(invoice => invoice.Transactions)
            .WithOne(transaction => transaction.Invoice)
            .HasForeignKey(transaction => transaction.InvoiceId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class BillingTransactionConfiguration : BaseEntityConfiguration<BillingTransaction>
{
    protected override void ConfigureEntity(EntityTypeBuilder<BillingTransaction> builder)
    {
        builder.ToTable("BillingTransactions");

        builder.Property(transaction => transaction.Status).HasConversion<int>();
        builder.Property(transaction => transaction.Currency).HasMaxLength(3).IsRequired();
        builder.Property(transaction => transaction.Provider).HasMaxLength(50).IsRequired();
        builder.Property(transaction => transaction.ProviderTransactionId).HasMaxLength(200);
        builder.Property(transaction => transaction.ProviderReference).HasMaxLength(200);
        builder.Property(transaction => transaction.FailureReason).HasMaxLength(DomainRules.ShortTextMaxLength);
        builder.Property(transaction => transaction.MetadataJson).HasMaxLength(4000);
        builder.Property(transaction => transaction.Amount).HasPrecision(18, 2);

        // A provider transaction is recorded at most once, which is the second line of
        // defence behind PaymentWebhookEvents against replayed webhooks.
        builder.HasIndex(transaction => new { transaction.Provider, transaction.ProviderTransactionId })
            .IsUnique()
            .HasFilter("[ProviderTransactionId] IS NOT NULL")
            .HasDatabaseName("UX_BillingTransactions_Provider_TransactionId");

        builder.HasIndex(transaction => new { transaction.TenantId, transaction.Status, transaction.CreatedAt })
            .HasDatabaseName("IX_BillingTransactions_TenantId_Status_CreatedAt");

        builder.HasIndex(transaction => transaction.TenantSubscriptionId)
            .HasDatabaseName("IX_BillingTransactions_SubscriptionId");

        builder.HasOne(transaction => transaction.Tenant)
            .WithMany()
            .HasForeignKey(transaction => transaction.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(transaction => transaction.Subscription)
            .WithMany()
            .HasForeignKey(transaction => transaction.TenantSubscriptionId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class PaymentWebhookEventConfiguration : BaseEntityConfiguration<PaymentWebhookEvent>
{
    protected override void ConfigureEntity(EntityTypeBuilder<PaymentWebhookEvent> builder)
    {
        builder.ToTable("PaymentWebhookEvents");

        builder.Property(webhook => webhook.Provider).HasMaxLength(50).IsRequired();
        builder.Property(webhook => webhook.EventId).HasMaxLength(200).IsRequired();
        builder.Property(webhook => webhook.EventType).HasMaxLength(100).IsRequired();
        builder.Property(webhook => webhook.Status).HasConversion<int>();
        builder.Property(webhook => webhook.Payload).HasMaxLength(8000);
        builder.Property(webhook => webhook.SignatureHeader).HasMaxLength(512);
        builder.Property(webhook => webhook.ProcessingError).HasMaxLength(DomainRules.ShortTextMaxLength);

        // THE idempotency guarantee: one row per provider event, ever.
        builder.HasIndex(webhook => new { webhook.Provider, webhook.EventId })
            .IsUnique()
            .HasDatabaseName("UX_PaymentWebhookEvents_Provider_EventId");

        builder.HasIndex(webhook => new { webhook.Status, webhook.ReceivedAt })
            .HasDatabaseName("IX_PaymentWebhookEvents_Status_ReceivedAt");

        builder.HasIndex(webhook => webhook.TenantId)
            .HasDatabaseName("IX_PaymentWebhookEvents_TenantId");
    }
}
