using Sentinel.Contracts;
using Sentinel.RiskEngine.Domain.Models;
using Sentinel.RiskEngine.Domain.Rules;
using Sentinel.RiskEngine.Domain.Scoring;
using Sentinel.SharedKernel;

namespace Sentinel.Domain.Tests;

public static class RiskContextFactory
{
    public static RiskContext PromptExample()
    {
        var now = new DateTimeOffset(2026, 9, 12, 14, 45, 0, TimeSpan.Zero);
        return new RiskContext
        {
            Transaction = Tx("TX-928382", 4850m, "US", Channel.WEB, false, "unknown", now),
            Profile = new CustomerBehaviourProfile
            {
                AccountId = "A-91828",
                CustomerId = "C-4411",
                HomeCountry = "GB",
                AverageTransactionAmount = Money.Gbp(87),
                MedianTransactionAmount = Money.Gbp(72),
                AverageDailySpend = Money.Gbp(140),
                CommonCountries = ["GB"],
                CommonDevices = ["D-UK-4411"],
                AccountOpenedAt = now.AddYears(-3),
                TransactionsPerDay = 2,
                LifetimeTransactionCount = 400
            },
            Velocity = new VelocitySnapshot
            {
                TransactionsLast1Minute = 4,
                TransactionsLast5Minutes = 7,
                TransactionsLast1Hour = 7,
                SpendLast5Minutes = Money.Gbp(5000),
                SpendLast1Hour = Money.Gbp(5000),
                SpendLast24Hours = Money.Gbp(5000),
                DistinctMerchantsLast10Minutes = 3,
                DistinctCountriesLast30Minutes = 2,
                FailedTransactionsLast15Minutes = 0
            },
            KnownDevices =
            [
                new KnownDeviceSnapshot
                {
                    DeviceId = "D-UK-4411",
                    FirstSeen = now.AddYears(-1),
                    LastSeen = now.AddDays(-1),
                    Trusted = true,
                    TransactionCount = 200
                }
            ],
            RecentTransactions = [],
            RuleParameters = Defaults(),
            RulesetVersion = DefaultRuleCatalog.InitialVersion,
            EvaluatedAt = now
        };
    }

    public static RiskContext Example(
        decimal amount = 40m,
        string country = "GB",
        Channel channel = Channel.POS,
        bool cardPresent = true,
        string? deviceId = "D-UK-4411",
        DateTimeOffset? timestamp = null,
        string[]? disable = null,
        Func<VelocitySnapshot, VelocitySnapshot>? velocity = null,
        Func<CustomerBehaviourProfile, CustomerBehaviourProfile>? profile = null,
        IReadOnlyList<HistoricalTransaction>? recent = null)
    {
        var now = timestamp ?? new DateTimeOffset(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);
        var vel = velocity?.Invoke(VelocitySnapshot.Empty("GBP")) ?? VelocitySnapshot.Empty("GBP");
        var customer = new CustomerBehaviourProfile
        {
            AccountId = "A-91828",
            CustomerId = "C-4411",
            HomeCountry = "GB",
            AverageTransactionAmount = Money.Gbp(87),
            MedianTransactionAmount = Money.Gbp(72),
            AverageDailySpend = Money.Gbp(140),
            CommonCountries = ["GB"],
            CommonDevices = ["D-UK-4411"],
            TypicalHoursUtc = [9, 12, 18],
            AccountOpenedAt = now.AddYears(-2),
            TransactionsPerDay = 2,
            LifetimeTransactionCount = 200
        };
        customer = profile?.Invoke(customer) ?? customer;

        var parameters = Defaults();
        if (disable is not null)
        {
            foreach (var code in disable)
            {
                var current = parameters[code];
                parameters[code] = current with { Enabled = false };
            }
        }

        return new RiskContext
        {
            Transaction = Tx("TX-1", amount, country, channel, cardPresent, deviceId, now),
            Profile = customer,
            Velocity = vel,
            KnownDevices =
            [
                new KnownDeviceSnapshot
                {
                    DeviceId = "D-UK-4411",
                    FirstSeen = now.AddMonths(-8),
                    LastSeen = now.AddDays(-1),
                    Trusted = true,
                    TransactionCount = 40
                }
            ],
            RecentTransactions = recent ?? [],
            RuleParameters = parameters,
            RulesetVersion = DefaultRuleCatalog.InitialVersion,
            EvaluatedAt = now
        };
    }

    public static TransactionSnapshot Tx(
        string id,
        decimal amount,
        string country,
        Channel channel,
        bool cardPresent,
        string? deviceId,
        DateTimeOffset timestamp) => new()
    {
        TransactionId = id,
        AccountId = "A-91828",
        CustomerId = "C-4411",
        Amount = Money.Gbp(amount),
        MerchantId = "M-TEST",
        MerchantCategory = MerchantCategory.Grocery,
        Country = country,
        City = country == "US" ? "New York" : "London",
        Timestamp = timestamp,
        CardPresent = cardPresent,
        Channel = channel,
        DeviceId = deviceId,
        IpAddress = "203.0.113.10",
        CorrelationId = "corr-1"
    };

    public static Dictionary<string, RuleParameterSet> Defaults() =>
        DefaultRuleCatalog.Defaults.ToDictionary(
            x => x.Code,
            x => new RuleParameterSet
            {
                RuleCode = x.Code,
                Enabled = x.Enabled,
                Score = x.Score,
                Parameters = x.Parameters
            });
}
