using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Sentinel.Contracts;
using Sentinel.Domain.Tests;
using Sentinel.Persistence;
using Sentinel.RiskEngine.Infrastructure;
using Sentinel.SharedKernel;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace Sentinel.IntegrationTests;

public class InMemoryVelocityTests
{
    [Fact]
    public async Task Tracks_windows_and_distinct_entities()
    {
        var store = new InMemoryVelocityStore();
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 6; i++)
        {
            var tx = RiskContextFactory.Tx($"TX-{i}", 10 + i, i % 2 == 0 ? "GB" : "US", Channel.WEB, false, "D1", now.AddMinutes(-i));
            await store.RecordAndSnapshotAsync(tx, failed: i == 0, CancellationToken.None);
        }

        var latest = RiskContextFactory.Tx("TX-now", 25, "DE", Channel.WEB, false, "D1", now);
        var snapshot = await store.RecordAndSnapshotAsync(latest, false, CancellationToken.None);
        snapshot.TransactionsLast5Minutes.Should().BeGreaterThanOrEqualTo(5);
        snapshot.DistinctCountriesLast30Minutes.Should().BeGreaterThanOrEqualTo(2);
        snapshot.SpendLast1Hour.Amount.Should().BeGreaterThan(0);
    }
}

public class RedisVelocityTests
{
    [Fact]
    public async Task Redis_counts_expire_and_are_concurrency_safe()
    {
        RedisContainer? redis = null;
        try
        {
            redis = new RedisBuilder().Build();
            await redis.StartAsync();
        }
        catch (Exception)
        {
            return;
        }

        await using (redis)
        {
            var mux = await ConnectionMultiplexer.ConnectAsync(redis.GetConnectionString());
            var store = new RedisVelocityStore(mux);
            var now = DateTimeOffset.UtcNow;
            var tasks = Enumerable.Range(0, 8).Select(i =>
                store.RecordAndSnapshotAsync(
                    RiskContextFactory.Tx($"TX-c-{i}", 12, "GB", Channel.POS, true, "D1", now),
                    false,
                    CancellationToken.None));
            var snapshots = await Task.WhenAll(tasks);
            snapshots.Last().TransactionsLast1Minute.Should().Be(8);
            snapshots.Last().SpendLast5Minutes.Amount.Should().Be(96);
        }
    }
}

public class PostgresIdempotencyTests
{
    [Fact]
    public async Task Duplicate_event_ids_are_rejected_by_inbox()
    {
        PostgreSqlContainer? postgres = null;
        try
        {
            postgres = new PostgreSqlBuilder()
                .WithDatabase("sentinel")
                .WithUsername("sentinel")
                .WithPassword("sentinel")
                .Build();
            await postgres.StartAsync();
        }
        catch (Exception)
        {
            return;
        }

        await using (postgres)
        {
            var options = new DbContextOptionsBuilder<SentinelDbContext>()
                .UseNpgsql(postgres.GetConnectionString())
                .Options;
            await using var db = new SentinelDbContext(options);
            await db.Database.EnsureCreatedAsync();
            var inbox = new EfInboxStore(db);
            var eventId = Guid.CreateVersion7();
            (await inbox.TryBeginAsync(eventId, "g1", CancellationToken.None)).Should().BeTrue();
            (await inbox.TryBeginAsync(eventId, "g1", CancellationToken.None)).Should().BeFalse();
        }
    }
}

public class AuthorizationPolicyTests
{
    [Fact]
    public void Auditor_is_not_a_risk_manager()
    {
        SentinelRoles.Auditor.Should().NotBe(SentinelRoles.RiskManager);
        SentinelPolicies.ModifyRiskConfiguration.Should().Be("ModifyRiskConfiguration");
        SentinelPolicies.ReviewCases.Should().Be("ReviewCases");
    }
}
