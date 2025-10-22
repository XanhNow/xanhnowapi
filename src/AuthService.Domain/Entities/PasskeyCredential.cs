namespace AuthService.Domain.Entities;

public class PasskeyCredential
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public byte[] DescriptorId { get; set; } = default!;
    public byte[] PublicKey { get; set; } = default!;
    public byte[] UserHandle { get; set; } = default!;
    public uint SignCount { get; set; }
    public string CredType { get; set; } = "public-key";
    public Guid Aaguid { get; set; }
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
}
