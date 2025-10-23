using System.Threading.RateLimiting;
using System.Text;
using AuthService.Application.Abstractions;
using AuthService.Application.Options;
using AuthService.Domain.Abstractions;
using AuthService.Domain.Entities;
using AuthService.Infrastructure.Messaging;
using AuthService.Infrastructure.Persistence;
using AuthService.Infrastructure.Security;
using AuthService.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Options
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection("Redis"));
builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection("Kafka"));
builder.Services.Configure<PasskeyOptions>(builder.Configuration.GetSection("Passkey"));

// DbContext (pool)
builder.Services.AddDbContextPool<AppDbContext>(opt =>
{
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default"),
        npg => npg.EnableRetryOnFailure(5, TimeSpan.FromSeconds(2), null));
    opt.EnableSensitiveDataLogging(false);
});

// Identity
builder.Services.AddIdentityCore<ApplicationUser>(opt =>
{
    opt.User.RequireUniqueEmail = false;
    opt.Password.RequiredLength = 8;
    opt.Password.RequireDigit = true;
    opt.Password.RequireUppercase = true;
    opt.Password.RequireLowercase = true;
    opt.Password.RequireNonAlphanumeric = false;
})
.AddRoles<IdentityRole<Guid>>()
.AddEntityFrameworkStores<AppDbContext>()
.AddSignInManager();

// Auth: JWT
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()!;
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey));
builder.Services.AddAuthentication(o =>
{
    o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = jwt.Issuer,
        ValidateAudience = true, ValidAudience = jwt.Audience,
        ValidateIssuerSigningKey = true, IssuerSigningKey = key,
        ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30)
    };
});

// Redis cache
var redis = builder.Configuration.GetSection("Redis").Get<RedisOptions>()!;
builder.Services.AddStackExchangeRedisCache(o =>
{
    o.Configuration = redis.ConnectionString;
    o.InstanceName = redis.InstanceName;
});

// Kafka EventBus
var kopt = builder.Configuration.GetSection("Kafka").Get<KafkaOptions>()!;
builder.Services.AddSingleton<IEventBus>(_ => new KafkaEventBus(kopt.BootstrapServers, kopt.ClientId));

// DI
builder.Services.AddScoped(typeof(AuthService.Domain.Abstractions.IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<AuthService.Domain.Abstractions.IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService.Infrastructure.Services.AuthService>();
builder.Services.AddScoped<PasskeyService>();

// CORS (dev-friendly)
builder.Services.AddCors(p => p.AddDefaultPolicy(b => b
    .AllowAnyHeader().AllowAnyMethod().AllowCredentials()
    .SetIsOriginAllowed(_ => true)));

// Rate limit
builder.Services.AddRateLimiter(_ => _.AddFixedWindowLimiter("auth",
    options => { options.Window = TimeSpan.FromSeconds(10); options.PermitLimit = 20; }));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
//
builder.Services.AddHealthChecks();

var app = builder.Build();

// migrate on startup (dev)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/healthz");

app.Run();
