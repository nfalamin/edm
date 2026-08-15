using System;
using System.IO;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using EDM.ControlPlane.Api.Data;
using EDM.ControlPlane.Api.Middleware;
using EDM.ControlPlane.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Database Configuration
string? postgresConn = builder.Configuration.GetConnectionString("PostgreSql");
string? sqliteConn = builder.Configuration.GetConnectionString("Sqlite") ?? "Data Source=controlplane.db";

builder.Services.AddDbContext<ControlPlaneDbContext>(options =>
{
    if (!string.IsNullOrWhiteSpace(postgresConn))
    {
        options.UseNpgsql(postgresConn);
    }
    else
    {
        options.UseSqlite(sqliteConn);
    }
});

// Register Core Domain & Security Services
builder.Services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
builder.Services.AddSingleton<IPrivacySafeDeviceService, PrivacySafeDeviceService>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<IAuditLoggingService, AuditLoggingService>();
builder.Services.AddScoped<IBanEnforcementService, BanEnforcementService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Configure CORS for Dashboard
builder.Services.AddCors(options =>
{
    options.AddPolicy("DashboardCorsPolicy", policy =>
    {
        policy.WithOrigins(
            "http://localhost:5000",
            "https://localhost:5001",
            "http://127.0.0.1:5500",
            "http://localhost:3000",
            "https://control.edm.local")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Configure JWT Bearer Authentication
string jwtSecret = builder.Configuration["Jwt:SecretKey"] ?? "EDM_Development_Super_Secret_Key_For_Jwt_Signing_2026_Minimum_256_Bits!";
string jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "EDM.ControlPlane";
string jwtAudience = builder.Configuration["Jwt:Audience"] ?? "EDM.Clients";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // Configurable per deployment environment
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// Configure Role-Based Authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireSuperAdmin", policy => policy.RequireRole("SUPER_ADMIN"));
    options.AddPolicy("RequireAdmin", policy => policy.RequireRole("SUPER_ADMIN", "ADMIN"));
    options.AddPolicy("RequireSupport", policy => policy.RequireRole("SUPER_ADMIN", "ADMIN", "SUPPORT"));
    options.AddPolicy("RequireReleaseManager", policy => policy.RequireRole("SUPER_ADMIN", "ADMIN", "RELEASE_MANAGER"));
    options.AddPolicy("RequireAnalyst", policy => policy.RequireRole("SUPER_ADMIN", "ADMIN", "ANALYST"));
});

// Configure Rate Limiting Middleware
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Login rate limiter: max 10 requests per minute per IP
    options.AddPolicy("AuthRateLimit", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 10,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));
});

var app = builder.Build();

// Ensure DB is initialized
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
    db.Database.EnsureCreated();

    if (!db.Releases.Any(r => r.Platform == EDM.ControlPlane.Api.Models.ClientType.DesktopWindows && !r.IsWithdrawn))
    {
        var rel = new EDM.ControlPlane.Api.Models.Release
        {
            Id = Guid.NewGuid(),
            Platform = EDM.ControlPlane.Api.Models.ClientType.DesktopWindows,
            Version = "2.0.0",
            MinimumSupportedVersion = "1.0.0",
            Title = "EDM 2.0.0 Official Release",
            ReleaseNotes = "High performance multi-stream download engine.",
            PublishedAtUtc = DateTime.UtcNow,
            IsMandatory = false,
            Severity = EDM.ControlPlane.Api.Models.ReleaseSeverity.Standard
        };
        rel.Artifacts.Add(new EDM.ControlPlane.Api.Models.ReleaseArtifact
        {
            Id = Guid.NewGuid(),
            ReleaseId = rel.Id,
            ArtifactName = "EDM_Setup.exe",
            DownloadUrl = "https://releases.edm.com/desktop/EDM_Setup.exe",
            Sha256Hash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            FileSizeBytes = 3500000
        });
        db.Releases.Add(rel);
        db.SaveChanges();
    }
}

// Middleware Pipeline
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseCors("DashboardCorsPolicy");

// Serve Dashboard Static Files if present
string dashboardPath = Path.Combine(builder.Environment.ContentRootPath, "..", "EDM.ControlPlane.Dashboard");
if (Directory.Exists(dashboardPath))
{
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = new PhysicalFileProvider(dashboardPath),
        RequestPath = ""
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(dashboardPath),
        RequestPath = ""
    });
}

app.UseRateLimiter();

app.UseAuthentication();

app.UseMiddleware<BanEnforcementMiddleware>();

app.UseAuthorization();

app.MapControllers();

app.Run();

namespace EDM.ControlPlane.Api
{
    public partial class Program { }
}
