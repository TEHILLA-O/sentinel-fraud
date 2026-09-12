using Microsoft.AspNetCore.SignalR;
using Sentinel.Contracts;
using StackExchange.Redis;

namespace Sentinel.Web.Realtime;

public sealed class LiveDecisionListener : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IHubContext<LiveDecisionHub> _hub;

    public LiveDecisionListener(IConnectionMultiplexer redis, IHubContext<LiveDecisionHub> hub)
    {
        _redis = redis;
        _hub = hub;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var subscriber = _redis.GetSubscriber();
        await subscriber.SubscribeAsync(RedisChannel.Literal(RedisChannels.LiveDecisions), async (_, value) =>
        {
            await _hub.Clients.All.SendAsync(LiveDecisionHub.Decisions, value.ToString(), stoppingToken);
        });
        await subscriber.SubscribeAsync(RedisChannel.Literal(RedisChannels.LiveAlerts), async (_, value) =>
        {
            await _hub.Clients.All.SendAsync(LiveDecisionHub.Alerts, value.ToString(), stoppingToken);
        });

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }
}
