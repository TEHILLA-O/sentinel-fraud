namespace Sentinel.Eventing;

public interface IEventPublisher
{
    Task PublishAsync(string topic, string key, string payload, string correlationId, CancellationToken cancellationToken);
}
