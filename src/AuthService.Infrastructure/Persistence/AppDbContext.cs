using AuthService.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) {}

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasskeyCredential> PasskeyCredentials => Set<PasskeyCredential>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<ApplicationUser>(e =>
        {
            e.HasIndex(x => x.PhoneNumber).IsUnique();
        });

        b.Entity<RefreshToken>(e =>
        {
            e.HasIndex(x => x.Token).IsUnique();
            e.Property(x => x.Token).HasMaxLength(200);
            e.Property(x => x.JwtId).HasMaxLength(200);
        });

        b.Entity<PasskeyCredential>(e =>
        {
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.DescriptorId).IsUnique();
        });
    }
}
