using AuthService.Domain.Abstractions;
using Confluent.Kafka;
using System.Text.Json;

namespace AuthService.Infrastructure.Messaging;

public class KafkaEventBus : IEventBus, IDisposable
{
    private readonly IProducer<string, string> _producer;

    public KafkaEventBus(string bootstrapServers, string clientId)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            ClientId = clientId,
            Acks = Acks.All,
            EnableIdempotence = true,
            LingerMs = 5,
            BatchSize = 64 * 1024
        };
        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublishAsync<T>(string topic, T message, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(message);
        await _producer.ProduceAsync(topic, new Message<string, string>
        {
            Key = Guid.NewGuid().ToString("N"),
            Value = payload
        }, ct);
    }

    public void Dispose() => _producer?.Dispose();
}
