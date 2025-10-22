namespace AuthService.Domain.Abstractions;

public interface IEventBus
{
    Task PublishAsync<T>(string topic, T message, CancellationToken ct = default);
}
