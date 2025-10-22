namespace AuthService.Application.Options;

public class KafkaOptions
{
    public string BootstrapServers { get; set; } = default!;
    public string ClientId { get; set; } = "auth-service";
}
