using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentinel.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "analyst_notes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Author = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analyst_notes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "audit_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Actor = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    EntityId = table.Column<string>(type: "text", nullable: false),
                    DetailsJson = table.Column<string>(type: "text", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CorrelationId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "case_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "text", nullable: false),
                    ToStatus = table.Column<string>(type: "text", nullable: false),
                    Actor = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_case_history", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "customer_profiles",
                columns: table => new
                {
                    AccountId = table.Column<string>(type: "text", nullable: false),
                    CustomerId = table.Column<string>(type: "text", nullable: false),
                    HomeCountry = table.Column<string>(type: "text", nullable: false),
                    AverageTransactionAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MedianTransactionAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AverageDailySpend = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    CommonCountriesJson = table.Column<string>(type: "text", nullable: false),
                    CommonMerchantsJson = table.Column<string>(type: "text", nullable: false),
                    CommonCategoriesJson = table.Column<string>(type: "text", nullable: false),
                    CommonDevicesJson = table.Column<string>(type: "text", nullable: false),
                    TypicalHoursJson = table.Column<string>(type: "text", nullable: false),
                    TransactionsPerDay = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    AccountOpenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LifetimeTransactionCount = table.Column<int>(type: "integer", nullable: false),
                    RecentAmountsJson = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_profiles", x => x.AccountId);
                });

            migrationBuilder.CreateTable(
                name: "fraud_cases",
                columns: table => new
                {
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<string>(type: "text", nullable: false),
                    CustomerId = table.Column<string>(type: "text", nullable: false),
                    AccountId = table.Column<string>(type: "text", nullable: false),
                    RiskScore = table.Column<int>(type: "integer", nullable: false),
                    RiskLevel = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    AssignedAnalyst = table.Column<string>(type: "text", nullable: true),
                    AnalystDecision = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fraud_cases", x => x.CaseId);
                });

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Consumer = table.Column<string>(type: "text", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbox_messages", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "known_devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<string>(type: "text", nullable: false),
                    DeviceId = table.Column<string>(type: "text", nullable: false),
                    FirstSeen = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeen = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Trusted = table.Column<bool>(type: "boolean", nullable: false),
                    TransactionCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_known_devices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Topic = table.Column<string>(type: "text", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "risk_decisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<string>(type: "text", nullable: false),
                    AccountId = table.Column<string>(type: "text", nullable: false),
                    CustomerId = table.Column<string>(type: "text", nullable: false),
                    RawScore = table.Column<int>(type: "integer", nullable: false),
                    RiskScore = table.Column<int>(type: "integer", nullable: false),
                    RiskLevel = table.Column<string>(type: "text", nullable: false),
                    Decision = table.Column<string>(type: "text", nullable: false),
                    TriggeredRulesJson = table.Column<string>(type: "text", nullable: false),
                    ReasonsJson = table.Column<string>(type: "text", nullable: false),
                    EvidenceJson = table.Column<string>(type: "text", nullable: false),
                    TransactionSnapshotJson = table.Column<string>(type: "text", nullable: false),
                    ProfileSnapshotJson = table.Column<string>(type: "text", nullable: false),
                    RuleConfigurationJson = table.Column<string>(type: "text", nullable: false),
                    RulesetVersion = table.Column<string>(type: "text", nullable: false),
                    ModelVersion = table.Column<string>(type: "text", nullable: false),
                    ProcessingTimeMs = table.Column<long>(type: "bigint", nullable: false),
                    CorrelationId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_risk_decisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "risk_rule_configurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RulesetVersion = table.Column<string>(type: "text", nullable: false),
                    RuleCode = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    ParametersJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_risk_rule_configurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ruleset_versions",
                columns: table => new
                {
                    Version = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ConfigurationJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ruleset_versions", x => x.Version);
                });

            migrationBuilder.CreateTable(
                name: "transactions",
                columns: table => new
                {
                    TransactionId = table.Column<string>(type: "text", nullable: false),
                    AccountId = table.Column<string>(type: "text", nullable: false),
                    CustomerId = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    MerchantId = table.Column<string>(type: "text", nullable: false),
                    MerchantCategory = table.Column<string>(type: "text", nullable: false),
                    Country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    City = table.Column<string>(type: "text", nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CardPresent = table.Column<bool>(type: "boolean", nullable: false),
                    Channel = table.Column<string>(type: "text", nullable: false),
                    DeviceId = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    CorrelationId = table.Column<string>(type: "text", nullable: false),
                    IngestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transactions", x => x.TransactionId);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_analyst_notes_CaseId",
                table: "analyst_notes",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_EntityType_EntityId",
                table: "audit_events",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_OccurredAt",
                table: "audit_events",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_case_history_CaseId",
                table: "case_history",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_customer_profiles_CustomerId",
                table: "customer_profiles",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_fraud_cases_CreatedAt",
                table: "fraud_cases",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_fraud_cases_CustomerId",
                table: "fraud_cases",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_fraud_cases_Status",
                table: "fraud_cases",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_fraud_cases_TransactionId",
                table: "fraud_cases",
                column: "TransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inbox_messages_EventId_Consumer",
                table: "inbox_messages",
                columns: new[] { "EventId", "Consumer" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_known_devices_CustomerId_DeviceId",
                table: "known_devices",
                columns: new[] { "CustomerId", "DeviceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_CreatedAt",
                table: "outbox_messages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_PublishedAt",
                table: "outbox_messages",
                column: "PublishedAt");

            migrationBuilder.CreateIndex(
                name: "IX_risk_decisions_AccountId",
                table: "risk_decisions",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_risk_decisions_CreatedAt",
                table: "risk_decisions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_risk_decisions_Decision_CreatedAt",
                table: "risk_decisions",
                columns: new[] { "Decision", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_risk_decisions_RulesetVersion",
                table: "risk_decisions",
                column: "RulesetVersion");

            migrationBuilder.CreateIndex(
                name: "IX_risk_decisions_TransactionId",
                table: "risk_decisions",
                column: "TransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_risk_rule_configurations_RulesetVersion_RuleCode",
                table: "risk_rule_configurations",
                columns: new[] { "RulesetVersion", "RuleCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ruleset_versions_IsActive",
                table: "ruleset_versions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_AccountId",
                table: "transactions",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_CorrelationId",
                table: "transactions",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_CustomerId",
                table: "transactions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_Timestamp",
                table: "transactions",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "analyst_notes");

            migrationBuilder.DropTable(
                name: "audit_events");

            migrationBuilder.DropTable(
                name: "case_history");

            migrationBuilder.DropTable(
                name: "customer_profiles");

            migrationBuilder.DropTable(
                name: "fraud_cases");

            migrationBuilder.DropTable(
                name: "inbox_messages");

            migrationBuilder.DropTable(
                name: "known_devices");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "risk_decisions");

            migrationBuilder.DropTable(
                name: "risk_rule_configurations");

            migrationBuilder.DropTable(
                name: "ruleset_versions");

            migrationBuilder.DropTable(
                name: "transactions");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
