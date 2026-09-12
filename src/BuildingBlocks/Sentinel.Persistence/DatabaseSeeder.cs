using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sentinel.Contracts;
using Sentinel.RiskEngine.Domain.Scoring;
using Sentinel.SharedKernel;

namespace Sentinel.Persistence;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(SentinelDbContext db, CancellationToken cancellationToken = default)
    {
        if (!await db.Users.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            db.Users.AddRange(
                User("analyst@sentinel.local", "Alex Analyst", SentinelRoles.FraudAnalyst),
                User("senior@sentinel.local", "Sam Senior", SentinelRoles.SeniorAnalyst),
                User("risk@sentinel.local", "Riley Risk", SentinelRoles.RiskManager),
                User("admin@sentinel.local", "Avery Admin", SentinelRoles.Administrator),
                User("auditor@sentinel.local", "Arden Auditor", SentinelRoles.Auditor));
        }

        if (!await db.RulesetVersions.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            var version = DefaultRuleCatalog.InitialVersion;
            db.RulesetVersions.Add(new RulesetVersionRecord
            {
                Version = version,
                IsActive = true,
                ConfigurationJson = JsonSerializer.Serialize(DefaultRuleCatalog.Defaults),
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "system"
            });

            foreach (var rule in DefaultRuleCatalog.Defaults)
            {
                db.RiskRuleConfigurations.Add(new RiskRuleConfigurationRecord
                {
                    Id = Guid.CreateVersion7(),
                    RulesetVersion = version,
                    RuleCode = rule.Code,
                    Enabled = rule.Enabled,
                    Score = rule.Score,
                    ParametersJson = JsonSerializer.Serialize(rule.Parameters)
                });
            }
        }

        if (!await db.CustomerProfiles.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            var opened = DateTimeOffset.UtcNow.AddYears(-3);
            db.CustomerProfiles.Add(Profile(
                "A-91828",
                "C-4411",
                "GB",
                87m,
                72m,
                140m,
                ["GB"],
                ["M-TESCO", "M-SAINSBURY"],
                [MerchantCategory.Grocery, MerchantCategory.Fuel, MerchantCategory.Restaurant],
                ["D-UK-4411"],
                [8, 9, 12, 13, 17, 18, 19],
                2.4m,
                opened,
                640));

            db.KnownDevices.Add(new KnownDeviceRecord
            {
                Id = Guid.CreateVersion7(),
                CustomerId = "C-4411",
                DeviceId = "D-UK-4411",
                FirstSeen = opened.AddMonths(1),
                LastSeen = DateTimeOffset.UtcNow.AddDays(-1),
                Trusted = true,
                TransactionCount = 410
            });

            db.CustomerProfiles.Add(Profile(
                "A-22011",
                "C-2201",
                "GB",
                46m,
                38m,
                90m,
                ["GB", "IE"],
                ["M-BP", "M-PRET"],
                [MerchantCategory.Fuel, MerchantCategory.Restaurant],
                ["D-UK-2201"],
                [7, 8, 12, 18],
                1.6m,
                DateTimeOffset.UtcNow.AddDays(-8),
                11));
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static UserRecord User(string email, string name, string role) => new()
    {
        Id = Guid.CreateVersion7(),
        Email = email,
        DisplayName = name,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Sentinel!23"),
        Role = role,
        Active = true
    };

    private static CustomerProfileRecord Profile(
        string accountId,
        string customerId,
        string home,
        decimal average,
        decimal median,
        decimal daily,
        string[] countries,
        string[] merchants,
        MerchantCategory[] categories,
        string[] devices,
        int[] hours,
        decimal perDay,
        DateTimeOffset opened,
        int count) => new()
    {
        AccountId = accountId,
        CustomerId = customerId,
        HomeCountry = home,
        AverageTransactionAmount = average,
        MedianTransactionAmount = median,
        AverageDailySpend = daily,
        Currency = "GBP",
        CommonCountriesJson = JsonSerializer.Serialize(countries),
        CommonMerchantsJson = JsonSerializer.Serialize(merchants),
        CommonCategoriesJson = JsonSerializer.Serialize(categories),
        CommonDevicesJson = JsonSerializer.Serialize(devices),
        TypicalHoursJson = JsonSerializer.Serialize(hours),
        TransactionsPerDay = perDay,
        AccountOpenedAt = opened,
        LifetimeTransactionCount = count,
        RecentAmountsJson = JsonSerializer.Serialize(Enumerable.Repeat(average, 12).ToArray()),
        UpdatedAt = DateTimeOffset.UtcNow
    };
}
