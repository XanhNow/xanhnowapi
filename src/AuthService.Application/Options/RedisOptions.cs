namespace AuthService.Application.Options;

public class RedisOptions
{
    public string ConnectionString { get; set; } = default!;
    public string InstanceName { get; set; } = "auth:";
}
