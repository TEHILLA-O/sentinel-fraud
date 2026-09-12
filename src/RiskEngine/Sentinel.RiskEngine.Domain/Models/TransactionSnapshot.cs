using Sentinel.Contracts;
using Sentinel.SharedKernel;

namespace Sentinel.RiskEngine.Domain.Models;

public sealed record TransactionSnapshot
{
    public required string TransactionId { get; init; }

    public required string AccountId { get; init; }

    public required string CustomerId { get; init; }

    public required Money Amount { get; init; }

    public required string MerchantId { get; init; }

    public required MerchantCategory MerchantCategory { get; init; }

    public required string Country { get; init; }

    public string? City { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public required bool CardPresent { get; init; }

    public required Channel Channel { get; init; }

    public string? DeviceId { get; init; }

    public string? IpAddress { get; init; }

    public required string CorrelationId { get; init; }
}

public sealed record HistoricalTransaction
{
    public required string TransactionId { get; init; }

    public required string Country { get; init; }

    public string? City { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public required Channel Channel { get; init; }

    public required Money Amount { get; init; }

    public required bool Approved { get; init; }
}

public sealed record KnownDeviceSnapshot
{
    public required string DeviceId { get; init; }

    public required DateTimeOffset FirstSeen { get; init; }

    public required DateTimeOffset LastSeen { get; init; }

    public required bool Trusted { get; init; }

    public required int TransactionCount { get; init; }
}

public sealed record CustomerBehaviourProfile
{
    public required string AccountId { get; init; }

    public required string CustomerId { get; init; }

    public required string HomeCountry { get; init; }

    public required Money AverageTransactionAmount { get; init; }

    public required Money MedianTransactionAmount { get; init; }

    public required Money AverageDailySpend { get; init; }

    public IReadOnlyList<string> CommonCountries { get; init; } = [];

    public IReadOnlyList<string> CommonMerchants { get; init; } = [];

    public IReadOnlyList<MerchantCategory> CommonCategories { get; init; } = [];

    public IReadOnlyList<string> CommonDevices { get; init; } = [];

    public IReadOnlyList<int> TypicalHoursUtc { get; init; } = [];

    public required decimal TransactionsPerDay { get; init; }

    public required DateTimeOffset AccountOpenedAt { get; init; }

    public required int LifetimeTransactionCount { get; init; }

    public static CustomerBehaviourProfile Empty(string accountId, string customerId, string homeCountry, DateTimeOffset openedAt, string currency) =>
        new()
        {
            AccountId = accountId,
            CustomerId = customerId,
            HomeCountry = homeCountry,
            AverageTransactionAmount = Money.Zero(currency),
            MedianTransactionAmount = Money.Zero(currency),
            AverageDailySpend = Money.Zero(currency),
            AccountOpenedAt = openedAt,
            TransactionsPerDay = 0,
            LifetimeTransactionCount = 0
        };
}

public sealed record VelocitySnapshot
{
    public required int TransactionsLast1Minute { get; init; }

    public required int TransactionsLast5Minutes { get; init; }

    public required int TransactionsLast1Hour { get; init; }

    public required Money SpendLast5Minutes { get; init; }

    public required Money SpendLast1Hour { get; init; }

    public required Money SpendLast24Hours { get; init; }

    public required int DistinctMerchantsLast10Minutes { get; init; }

    public required int DistinctCountriesLast30Minutes { get; init; }

    public required int FailedTransactionsLast15Minutes { get; init; }

    public static VelocitySnapshot Empty(string currency) => new()
    {
        TransactionsLast1Minute = 0,
        TransactionsLast5Minutes = 0,
        TransactionsLast1Hour = 0,
        SpendLast5Minutes = Money.Zero(currency),
        SpendLast1Hour = Money.Zero(currency),
        SpendLast24Hours = Money.Zero(currency),
        DistinctMerchantsLast10Minutes = 0,
        DistinctCountriesLast30Minutes = 0,
        FailedTransactionsLast15Minutes = 0
    };
}

public sealed record RuleParameterSet
{
    public required string RuleCode { get; init; }

    public required bool Enabled { get; init; }

    public required int Score { get; init; }

    public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>();

    public decimal GetDecimal(string key, decimal fallback) =>
        Parameters.TryGetValue(key, out var raw) && decimal.TryParse(raw, out var value) ? value : fallback;

    public int GetInt(string key, int fallback) =>
        Parameters.TryGetValue(key, out var raw) && int.TryParse(raw, out var value) ? value : fallback;

    public double GetDouble(string key, double fallback) =>
        Parameters.TryGetValue(key, out var raw) && double.TryParse(raw, out var value) ? value : fallback;
}
