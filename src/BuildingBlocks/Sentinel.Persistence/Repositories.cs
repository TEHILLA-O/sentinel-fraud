using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sentinel.Contracts;
using Sentinel.RiskEngine.Application;
using Sentinel.RiskEngine.Domain.Models;
using Sentinel.RiskEngine.Domain.Rules;
using Sentinel.SharedKernel;

namespace Sentinel.Persistence;

public sealed class EfInboxStore : IInboxStore
{
    private readonly SentinelDbContext _db;

    public EfInboxStore(SentinelDbContext db) => _db = db;

    public async Task<bool> TryBeginAsync(Guid eventId, string consumer, CancellationToken cancellationToken)
    {
        if (await _db.InboxMessages.AnyAsync(x => x.EventId == eventId, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        _db.InboxMessages.Add(new InboxMessageRecord
        {
            EventId = eventId,
            Consumer = consumer,
            ReceivedAt = DateTimeOffset.UtcNow
        });

        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            return false;
        }
    }

    public async Task CompleteAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var row = await _db.InboxMessages.FirstOrDefaultAsync(x => x.EventId == eventId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            return;
        }

        row.CompletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed class EfOutboxStore : IOutboxStore
{
    private readonly SentinelDbContext _db;

    public EfOutboxStore(SentinelDbContext db) => _db = db;

    public async Task EnqueueAsync(string topic, string key, string payload, string eventType, CancellationToken cancellationToken)
    {
        _db.OutboxMessages.Add(new OutboxMessageRecord
        {
            Id = Guid.CreateVersion7(),
            Topic = topic,
            Key = key,
            Payload = payload,
            EventType = eventType,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OutboxRecord>> DequeueBatchAsync(int take, CancellationToken cancellationToken)
    {
        var rows = await _db.OutboxMessages
            .Where(x => x.PublishedAt == null)
            .OrderBy(x => x.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(x => new OutboxRecord(x.Id, x.Topic, x.Key, x.Payload, x.EventType, x.CreatedAt)).ToList();
    }

    public async Task MarkPublishedAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await _db.OutboxMessages
            .Where(x => ids.Contains(x.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.PublishedAt, now), cancellationToken)
            .ConfigureAwait(false);
    }
}

public sealed class EfRiskDecisionRepository : IRiskDecisionRepository
{
    private readonly SentinelDbContext _db;

    public EfRiskDecisionRepository(SentinelDbContext db) => _db = db;

    public Task<bool> ExistsForTransactionAsync(string transactionId, CancellationToken cancellationToken) =>
        _db.RiskDecisions.AnyAsync(x => x.TransactionId == transactionId, cancellationToken);

    public async Task SaveAsync(PersistedDecision decision, CancellationToken cancellationToken)
    {
        var tx = decision.Transaction;
        if (!await _db.Transactions.AnyAsync(x => x.TransactionId == tx.TransactionId, cancellationToken).ConfigureAwait(false))
        {
            _db.Transactions.Add(new TransactionRecord
            {
                TransactionId = tx.TransactionId,
                AccountId = tx.AccountId,
                CustomerId = tx.CustomerId,
                Amount = tx.Amount.Amount,
                Currency = tx.Amount.Currency,
                MerchantId = tx.MerchantId,
                MerchantCategory = tx.MerchantCategory,
                Country = tx.Country,
                City = tx.City,
                Timestamp = tx.Timestamp,
                CardPresent = tx.CardPresent,
                Channel = tx.Channel,
                DeviceId = tx.DeviceId,
                IpAddress = tx.IpAddress,
                CorrelationId = tx.CorrelationId,
                IngestedAt = decision.CreatedAt
            });
        }

        _db.RiskDecisions.Add(new RiskDecisionRecord
        {
            Id = decision.Id,
            TransactionId = tx.TransactionId,
            AccountId = tx.AccountId,
            CustomerId = tx.CustomerId,
            RawScore = decision.Evaluation.RawScore,
            RiskScore = decision.Evaluation.RiskScore,
            RiskLevel = decision.Evaluation.RiskLevel,
            Decision = decision.Evaluation.Decision,
            TriggeredRulesJson = JsonSerializer.Serialize(decision.Evaluation.TriggeredRules),
            ReasonsJson = JsonSerializer.Serialize(decision.Evaluation.Reasons),
            EvidenceJson = JsonSerializer.Serialize(decision.Evaluation.Evidence),
            TransactionSnapshotJson = JsonSerializer.Serialize(tx),
            ProfileSnapshotJson = decision.ProfileSnapshotJson,
            RuleConfigurationJson = JsonSerializer.Serialize(decision.Evaluation.RulesetVersion),
            RulesetVersion = decision.Evaluation.RulesetVersion,
            ModelVersion = decision.Evaluation.ModelVersion,
            ProcessingTimeMs = decision.Evaluation.ProcessingTimeMs,
            CorrelationId = decision.CorrelationId,
            CreatedAt = decision.CreatedAt
        });

        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            if (!await ExistsForTransactionAsync(tx.TransactionId, cancellationToken).ConfigureAwait(false))
            {
                throw;
            }
        }
    }

    public async Task<PersistedDecision?> GetByTransactionIdAsync(string transactionId, CancellationToken cancellationToken)
    {
        var row = await _db.RiskDecisions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TransactionId == transactionId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        var tx = JsonSerializer.Deserialize<TransactionSnapshot>(row.TransactionSnapshotJson)
                 ?? throw new InvalidOperationException("Stored transaction snapshot is invalid.");
        var rules = JsonSerializer.Deserialize<List<RiskRuleResult>>(row.TriggeredRulesJson) ?? [];
        var reasons = JsonSerializer.Deserialize<List<string>>(row.ReasonsJson) ?? [];
        var evidence = JsonSerializer.Deserialize<Dictionary<string, string>>(row.EvidenceJson) ?? [];

        return new PersistedDecision
        {
            Id = row.Id,
            Transaction = tx,
            Evaluation = new RiskEvaluation
            {
                RawScore = row.RawScore,
                RiskScore = row.RiskScore,
                RiskLevel = row.RiskLevel,
                Decision = row.Decision,
                TriggeredRules = rules,
                Reasons = reasons,
                Evidence = evidence,
                ProcessingTimeMs = row.ProcessingTimeMs,
                ModelVersion = row.ModelVersion,
                RulesetVersion = row.RulesetVersion,
                EvaluatedAt = row.CreatedAt
            },
            CorrelationId = row.CorrelationId,
            CreatedAt = row.CreatedAt,
            ProfileSnapshotJson = row.ProfileSnapshotJson
        };
    }
}

public sealed class EfRuleConfigurationProvider : IRuleConfigurationProvider
{
    private readonly SentinelDbContext _db;
    private ActiveRuleset? _cache;
    private DateTimeOffset _cacheUntil;

    public EfRuleConfigurationProvider(SentinelDbContext db) => _db = db;

    public async Task<ActiveRuleset> GetActiveAsync(CancellationToken cancellationToken)
    {
        if (_cache is not null && _cacheUntil > DateTimeOffset.UtcNow)
        {
            return _cache;
        }

        var version = await _db.RulesetVersions.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.CreatedAt)
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);

        var rows = await _db.RiskRuleConfigurations.AsNoTracking()
            .Where(x => x.RulesetVersion == version.Version)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var parameters = rows.ToDictionary(
            x => x.RuleCode,
            x => new RuleParameterSet
            {
                RuleCode = x.RuleCode,
                Enabled = x.Enabled,
                Score = x.Score,
                Parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(x.ParametersJson)
                             ?? new Dictionary<string, string>()
            },
            StringComparer.Ordinal);

        _cache = new ActiveRuleset(version.Version, parameters);
        _cacheUntil = DateTimeOffset.UtcNow.AddSeconds(15);
        return _cache;
    }

    public void Invalidate()
    {
        _cache = null;
        _cacheUntil = DateTimeOffset.MinValue;
    }
}

public sealed class EfCustomerProfileStore : ICustomerProfileStore
{
    private readonly SentinelDbContext _db;

    public EfCustomerProfileStore(SentinelDbContext db) => _db = db;

    public async Task<CustomerBehaviourProfile> GetAsync(
        string accountId,
        string customerId,
        string currency,
        CancellationToken cancellationToken)
    {
        var row = await _db.CustomerProfiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.AccountId == accountId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return CustomerBehaviourProfile.Empty(accountId, customerId, "GB", DateTimeOffset.UtcNow, currency);
        }

        return new CustomerBehaviourProfile
        {
            AccountId = row.AccountId,
            CustomerId = row.CustomerId,
            HomeCountry = row.HomeCountry,
            AverageTransactionAmount = new Money(row.AverageTransactionAmount, row.Currency),
            MedianTransactionAmount = new Money(row.MedianTransactionAmount, row.Currency),
            AverageDailySpend = new Money(row.AverageDailySpend, row.Currency),
            CommonCountries = JsonSerializer.Deserialize<List<string>>(row.CommonCountriesJson) ?? [],
            CommonMerchants = JsonSerializer.Deserialize<List<string>>(row.CommonMerchantsJson) ?? [],
            CommonCategories = JsonSerializer.Deserialize<List<MerchantCategory>>(row.CommonCategoriesJson) ?? [],
            CommonDevices = JsonSerializer.Deserialize<List<string>>(row.CommonDevicesJson) ?? [],
            TypicalHoursUtc = JsonSerializer.Deserialize<List<int>>(row.TypicalHoursJson) ?? [],
            TransactionsPerDay = row.TransactionsPerDay,
            AccountOpenedAt = row.AccountOpenedAt,
            LifetimeTransactionCount = row.LifetimeTransactionCount
        };
    }

    public async Task<IReadOnlyList<KnownDeviceSnapshot>> GetDevicesAsync(string customerId, CancellationToken cancellationToken)
    {
        return await _db.KnownDevices.AsNoTracking()
            .Where(x => x.CustomerId == customerId)
            .Select(x => new KnownDeviceSnapshot
            {
                DeviceId = x.DeviceId,
                FirstSeen = x.FirstSeen,
                LastSeen = x.LastSeen,
                Trusted = x.Trusted,
                TransactionCount = x.TransactionCount
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<HistoricalTransaction>> GetRecentTransactionsAsync(
        string accountId,
        int take,
        CancellationToken cancellationToken)
    {
        var rows = await _db.Transactions.AsNoTracking()
            .Where(x => x.AccountId == accountId)
            .OrderByDescending(x => x.Timestamp)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var decisions = await _db.RiskDecisions.AsNoTracking()
            .Where(x => x.AccountId == accountId)
            .Select(x => new { x.TransactionId, x.Decision })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var lookup = decisions.ToDictionary(x => x.TransactionId, x => x.Decision);

        return rows.Select(x => new HistoricalTransaction
        {
            TransactionId = x.TransactionId,
            Country = x.Country,
            City = x.City,
            Timestamp = x.Timestamp,
            Channel = x.Channel,
            Amount = new Money(x.Amount, x.Currency),
            Approved = !lookup.TryGetValue(x.TransactionId, out var decision) || decision == RiskDecision.Approve
        }).ToList();
    }

    public async Task ApplyTransactionAsync(TransactionSnapshot transaction, RiskDecision decision, CancellationToken cancellationToken)
    {
        var row = await _db.CustomerProfiles
            .FirstOrDefaultAsync(x => x.AccountId == transaction.AccountId, cancellationToken)
            .ConfigureAwait(false);

        var amounts = new List<decimal>();
        if (row is null)
        {
            row = new CustomerProfileRecord
            {
                AccountId = transaction.AccountId,
                CustomerId = transaction.CustomerId,
                HomeCountry = transaction.Country,
                AverageTransactionAmount = transaction.Amount.Amount,
                MedianTransactionAmount = transaction.Amount.Amount,
                AverageDailySpend = transaction.Amount.Amount,
                Currency = transaction.Amount.Currency,
                CommonCountriesJson = "[]",
                CommonMerchantsJson = "[]",
                CommonCategoriesJson = "[]",
                CommonDevicesJson = "[]",
                TypicalHoursJson = "[]",
                TransactionsPerDay = 1,
                AccountOpenedAt = DateTimeOffset.UtcNow,
                LifetimeTransactionCount = 0,
                RecentAmountsJson = "[]",
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.CustomerProfiles.Add(row);
        }
        else
        {
            amounts = JsonSerializer.Deserialize<List<decimal>>(row.RecentAmountsJson) ?? [];
        }

        amounts.Add(transaction.Amount.Amount);
        if (amounts.Count > 100)
        {
            amounts.RemoveAt(0);
        }

        row.LifetimeTransactionCount += 1;
        row.AverageTransactionAmount = amounts.Average();
        var sorted = amounts.OrderBy(x => x).ToArray();
        row.MedianTransactionAmount = sorted[sorted.Length / 2];
        row.RecentAmountsJson = JsonSerializer.Serialize(amounts);
        row.UpdatedAt = DateTimeOffset.UtcNow;
        TouchList(row.CommonCountriesJson, transaction.Country, v => row.CommonCountriesJson = v);
        TouchList(row.CommonMerchantsJson, transaction.MerchantId, v => row.CommonMerchantsJson = v);
        TouchList(row.CommonCategoriesJson, transaction.MerchantCategory.ToString(), v => row.CommonCategoriesJson = JsonSerializer.Serialize(
            (JsonSerializer.Deserialize<List<string>>(v) ?? []).Select(Enum.Parse<MerchantCategory>).Take(8).ToList()));
        if (!string.IsNullOrWhiteSpace(transaction.DeviceId))
        {
            TouchList(row.CommonDevicesJson, transaction.DeviceId, v => row.CommonDevicesJson = v);
        }

        var hours = JsonSerializer.Deserialize<List<int>>(row.TypicalHoursJson) ?? [];
        hours.Add(transaction.Timestamp.UtcDateTime.Hour);
        row.TypicalHoursJson = JsonSerializer.Serialize(hours.TakeLast(48).ToList());

        if (!string.IsNullOrWhiteSpace(transaction.DeviceId))
        {
            var device = await _db.KnownDevices.FirstOrDefaultAsync(
                    x => x.CustomerId == transaction.CustomerId && x.DeviceId == transaction.DeviceId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (device is null)
            {
                _db.KnownDevices.Add(new KnownDeviceRecord
                {
                    Id = Guid.CreateVersion7(),
                    CustomerId = transaction.CustomerId,
                    DeviceId = transaction.DeviceId,
                    FirstSeen = transaction.Timestamp,
                    LastSeen = transaction.Timestamp,
                    Trusted = false,
                    TransactionCount = 1
                });
            }
            else
            {
                device.LastSeen = transaction.Timestamp;
                device.TransactionCount += 1;
                if (device.TransactionCount >= 5)
                {
                    device.Trusted = true;
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void TouchList(string json, string value, Action<string> assign)
    {
        var items = JsonSerializer.Deserialize<List<string>>(json) ?? [];
        items.RemoveAll(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase));
        items.Insert(0, value);
        assign(JsonSerializer.Serialize(items.Take(8).ToList()));
    }
}
