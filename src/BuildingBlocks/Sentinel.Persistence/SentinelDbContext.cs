using Microsoft.EntityFrameworkCore;
using Sentinel.Contracts;

namespace Sentinel.Persistence;

public sealed class SentinelDbContext : DbContext
{
    public SentinelDbContext(DbContextOptions<SentinelDbContext> options) : base(options)
    {
    }

    public DbSet<TransactionRecord> Transactions => Set<TransactionRecord>();

    public DbSet<RiskDecisionRecord> RiskDecisions => Set<RiskDecisionRecord>();

    public DbSet<RulesetVersionRecord> RulesetVersions => Set<RulesetVersionRecord>();

    public DbSet<RiskRuleConfigurationRecord> RiskRuleConfigurations => Set<RiskRuleConfigurationRecord>();

    public DbSet<FraudCaseRecord> Cases => Set<FraudCaseRecord>();

    public DbSet<CaseHistoryRecord> CaseHistory => Set<CaseHistoryRecord>();

    public DbSet<AnalystNoteRecord> AnalystNotes => Set<AnalystNoteRecord>();

    public DbSet<CustomerProfileRecord> CustomerProfiles => Set<CustomerProfileRecord>();

    public DbSet<KnownDeviceRecord> KnownDevices => Set<KnownDeviceRecord>();

    public DbSet<AuditEventRecord> AuditEvents => Set<AuditEventRecord>();

    public DbSet<OutboxMessageRecord> OutboxMessages => Set<OutboxMessageRecord>();

    public DbSet<InboxMessageRecord> InboxMessages => Set<InboxMessageRecord>();

    public DbSet<UserRecord> Users => Set<UserRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TransactionRecord>(entity =>
        {
            entity.ToTable("transactions");
            entity.HasKey(x => x.TransactionId);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.Currency).HasMaxLength(3);
            entity.Property(x => x.Country).HasMaxLength(2);
            entity.HasIndex(x => x.AccountId);
            entity.HasIndex(x => x.CustomerId);
            entity.HasIndex(x => x.Timestamp);
            entity.HasIndex(x => x.CorrelationId);
        });

        modelBuilder.Entity<RiskDecisionRecord>(entity =>
        {
            entity.ToTable("risk_decisions");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TransactionId).IsUnique();
            entity.HasIndex(x => x.AccountId);
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => new { x.Decision, x.CreatedAt });
            entity.HasIndex(x => x.RulesetVersion);
        });

        modelBuilder.Entity<RulesetVersionRecord>(entity =>
        {
            entity.ToTable("ruleset_versions");
            entity.HasKey(x => x.Version);
            entity.HasIndex(x => x.IsActive);
        });

        modelBuilder.Entity<RiskRuleConfigurationRecord>(entity =>
        {
            entity.ToTable("risk_rule_configurations");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.RulesetVersion, x.RuleCode }).IsUnique();
        });

        modelBuilder.Entity<FraudCaseRecord>(entity =>
        {
            entity.ToTable("fraud_cases");
            entity.HasKey(x => x.CaseId);
            entity.HasIndex(x => x.TransactionId).IsUnique();
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.CustomerId);
            entity.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<CaseHistoryRecord>(entity =>
        {
            entity.ToTable("case_history");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.CaseId);
        });

        modelBuilder.Entity<AnalystNoteRecord>(entity =>
        {
            entity.ToTable("analyst_notes");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.CaseId);
        });

        modelBuilder.Entity<CustomerProfileRecord>(entity =>
        {
            entity.ToTable("customer_profiles");
            entity.HasKey(x => x.AccountId);
            entity.HasIndex(x => x.CustomerId);
            entity.Property(x => x.AverageTransactionAmount).HasPrecision(18, 2);
            entity.Property(x => x.MedianTransactionAmount).HasPrecision(18, 2);
            entity.Property(x => x.AverageDailySpend).HasPrecision(18, 2);
            entity.Property(x => x.TransactionsPerDay).HasPrecision(18, 4);
        });

        modelBuilder.Entity<KnownDeviceRecord>(entity =>
        {
            entity.ToTable("known_devices");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.CustomerId, x.DeviceId }).IsUnique();
        });

        modelBuilder.Entity<AuditEventRecord>(entity =>
        {
            entity.ToTable("audit_events");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.OccurredAt);
            entity.HasIndex(x => new { x.EntityType, x.EntityId });
        });

        modelBuilder.Entity<OutboxMessageRecord>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.PublishedAt);
            entity.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<InboxMessageRecord>(entity =>
        {
            entity.ToTable("inbox_messages");
            entity.HasKey(x => x.EventId);
            entity.HasIndex(x => new { x.EventId, x.Consumer }).IsUnique();
        });

        modelBuilder.Entity<UserRecord>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<TransactionRecord>().Property(x => x.Channel).HasConversion<string>();
        modelBuilder.Entity<TransactionRecord>().Property(x => x.MerchantCategory).HasConversion<string>();
        modelBuilder.Entity<RiskDecisionRecord>().Property(x => x.Decision).HasConversion<string>();
        modelBuilder.Entity<RiskDecisionRecord>().Property(x => x.RiskLevel).HasConversion<string>();
        modelBuilder.Entity<FraudCaseRecord>().Property(x => x.Status).HasConversion<string>();
        modelBuilder.Entity<FraudCaseRecord>().Property(x => x.RiskLevel).HasConversion<string>();
        modelBuilder.Entity<FraudCaseRecord>().Property(x => x.AnalystDecision).HasConversion<string>();
        modelBuilder.Entity<CaseHistoryRecord>().Property(x => x.FromStatus).HasConversion<string>();
        modelBuilder.Entity<CaseHistoryRecord>().Property(x => x.ToStatus).HasConversion<string>();
    }
}
