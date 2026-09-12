using StackExchange.Redis;
using Sentinel.RiskEngine.Application;
using Sentinel.RiskEngine.Domain.Models;
using Sentinel.SharedKernel;

namespace Sentinel.RiskEngine.Infrastructure;

public sealed class RedisVelocityStore : IVelocityStore
{
    private readonly IConnectionMultiplexer _redis;

    public RedisVelocityStore(IConnectionMultiplexer redis) => _redis = redis;

    public async Task<VelocitySnapshot> RecordAndSnapshotAsync(
        TransactionSnapshot transaction,
        bool failed,
        CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase();
        var now = transaction.Timestamp.ToUnixTimeMilliseconds();
        var account = transaction.AccountId;
        var member = $"{transaction.TransactionId}:{transaction.Amount.ToMinorUnits()}";

        var txKey = $"vel:tx:{account}";
        var spendKey = $"vel:spend:{account}";
        var merchantKey = $"vel:merchants:{account}";
        var countryKey = $"vel:countries:{account}";
        var failKey = $"vel:fail:{account}";

        var batch = db.CreateBatch();
        var tasks = new List<Task>
        {
            batch.SortedSetAddAsync(txKey, transaction.TransactionId, now),
            batch.SortedSetAddAsync(spendKey, member, now),
            batch.SortedSetAddAsync(merchantKey, $"{now}:{transaction.MerchantId}", now),
            batch.SortedSetAddAsync(countryKey, $"{now}:{transaction.Country}", now),
            batch.KeyExpireAsync(txKey, TimeSpan.FromHours(36)),
            batch.KeyExpireAsync(spendKey, TimeSpan.FromHours(36)),
            batch.KeyExpireAsync(merchantKey, TimeSpan.FromHours(36)),
            batch.KeyExpireAsync(countryKey, TimeSpan.FromHours(36))
        };

        if (failed)
        {
            tasks.Add(batch.SortedSetAddAsync(failKey, transaction.TransactionId, now));
            tasks.Add(batch.KeyExpireAsync(failKey, TimeSpan.FromHours(6)));
        }

        var cutoff = now - TimeSpan.FromHours(36).TotalMilliseconds;
        tasks.Add(batch.SortedSetRemoveRangeByScoreAsync(txKey, double.NegativeInfinity, cutoff));
        tasks.Add(batch.SortedSetRemoveRangeByScoreAsync(spendKey, double.NegativeInfinity, cutoff));
        tasks.Add(batch.SortedSetRemoveRangeByScoreAsync(merchantKey, double.NegativeInfinity, cutoff));
        tasks.Add(batch.SortedSetRemoveRangeByScoreAsync(countryKey, double.NegativeInfinity, cutoff));
        batch.Execute();
        await Task.WhenAll(tasks).ConfigureAwait(false);

        var currency = transaction.Amount.Currency;
        var tx1 = await CountAsync(db, txKey, now, TimeSpan.FromMinutes(1)).ConfigureAwait(false);
        var tx5 = await CountAsync(db, txKey, now, TimeSpan.FromMinutes(5)).ConfigureAwait(false);
        var tx60 = await CountAsync(db, txKey, now, TimeSpan.FromHours(1)).ConfigureAwait(false);
        var spend5 = await SpendAsync(db, spendKey, now, TimeSpan.FromMinutes(5), currency).ConfigureAwait(false);
        var spend60 = await SpendAsync(db, spendKey, now, TimeSpan.FromHours(1), currency).ConfigureAwait(false);
        var spend24 = await SpendAsync(db, spendKey, now, TimeSpan.FromHours(24), currency).ConfigureAwait(false);
        var merchants = await DistinctSuffixAsync(db, merchantKey, now, TimeSpan.FromMinutes(10)).ConfigureAwait(false);
        var countries = await DistinctSuffixAsync(db, countryKey, now, TimeSpan.FromMinutes(30)).ConfigureAwait(false);
        var fails = failed
            ? await CountAsync(db, failKey, now, TimeSpan.FromMinutes(15)).ConfigureAwait(false)
            : await CountAsync(db, failKey, now, TimeSpan.FromMinutes(15)).ConfigureAwait(false);

        return new VelocitySnapshot
        {
            TransactionsLast1Minute = (int)tx1,
            TransactionsLast5Minutes = (int)tx5,
            TransactionsLast1Hour = (int)tx60,
            SpendLast5Minutes = spend5,
            SpendLast1Hour = spend60,
            SpendLast24Hours = spend24,
            DistinctMerchantsLast10Minutes = merchants,
            DistinctCountriesLast30Minutes = countries,
            FailedTransactionsLast15Minutes = (int)fails
        };
    }

    private static Task<long> CountAsync(IDatabase db, RedisKey key, long now, TimeSpan window) =>
        db.SortedSetLengthAsync(key, now - window.TotalMilliseconds, now);

    private static async Task<Money> SpendAsync(IDatabase db, RedisKey key, long now, TimeSpan window, string currency)
    {
        var entries = await db.SortedSetRangeByScoreAsync(key, now - window.TotalMilliseconds, now).ConfigureAwait(false);
        long minor = 0;
        foreach (var entry in entries)
        {
            var value = entry.ToString();
            var idx = value.LastIndexOf(':');
            if (idx >= 0 && long.TryParse(value[(idx + 1)..], out var parsed))
            {
                minor += parsed;
            }
        }

        return Money.FromMinorUnits(minor, currency);
    }

    private static async Task<int> DistinctSuffixAsync(IDatabase db, RedisKey key, long now, TimeSpan window)
    {
        var entries = await db.SortedSetRangeByScoreAsync(key, now - window.TotalMilliseconds, now).ConfigureAwait(false);
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            var value = entry.ToString();
            var idx = value.IndexOf(':');
            if (idx >= 0)
            {
                unique.Add(value[(idx + 1)..]);
            }
        }

        return unique.Count;
    }
}

public sealed class InMemoryVelocityStore : IVelocityStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, List<VelocityPoint>> _points = new(StringComparer.Ordinal);

    public Task<VelocitySnapshot> RecordAndSnapshotAsync(
        TransactionSnapshot transaction,
        bool failed,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!_points.TryGetValue(transaction.AccountId, out var list))
            {
                list = [];
                _points[transaction.AccountId] = list;
            }

            list.Add(new VelocityPoint(
                transaction.Timestamp,
                transaction.TransactionId,
                transaction.Amount,
                transaction.MerchantId,
                transaction.Country,
                failed));

            var now = transaction.Timestamp;
            var currency = transaction.Amount.Currency;
            return Task.FromResult(new VelocitySnapshot
            {
                TransactionsLast1Minute = Count(list, now, TimeSpan.FromMinutes(1)),
                TransactionsLast5Minutes = Count(list, now, TimeSpan.FromMinutes(5)),
                TransactionsLast1Hour = Count(list, now, TimeSpan.FromHours(1)),
                SpendLast5Minutes = Spend(list, now, TimeSpan.FromMinutes(5), currency),
                SpendLast1Hour = Spend(list, now, TimeSpan.FromHours(1), currency),
                SpendLast24Hours = Spend(list, now, TimeSpan.FromHours(24), currency),
                DistinctMerchantsLast10Minutes = Distinct(list, now, TimeSpan.FromMinutes(10), p => p.MerchantId),
                DistinctCountriesLast30Minutes = Distinct(list, now, TimeSpan.FromMinutes(30), p => p.Country),
                FailedTransactionsLast15Minutes = list.Count(p => p.Failed && p.Timestamp >= now - TimeSpan.FromMinutes(15))
            });
        }
    }

    private static int Count(List<VelocityPoint> list, DateTimeOffset now, TimeSpan window) =>
        list.Count(p => p.Timestamp >= now - window);

    private static Money Spend(List<VelocityPoint> list, DateTimeOffset now, TimeSpan window, string currency) =>
        list.Where(p => p.Timestamp >= now - window)
            .Aggregate(Money.Zero(currency), (acc, p) => acc + new Money(p.Amount.Amount, currency));

    private static int Distinct(List<VelocityPoint> list, DateTimeOffset now, TimeSpan window, Func<VelocityPoint, string> selector) =>
        list.Where(p => p.Timestamp >= now - window).Select(selector).Distinct(StringComparer.OrdinalIgnoreCase).Count();

    private readonly record struct VelocityPoint(
        DateTimeOffset Timestamp,
        string TransactionId,
        Money Amount,
        string MerchantId,
        string Country,
        bool Failed);
}
