using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
using EDM.ControlPlane.Api.Models;
using EDM.ControlPlane.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Strict Request Size Limits (50 MB Max)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 50 * 1024 * 1024;
    options.Limits.MaxRequestHeadersTotalSize = 64 * 1024;
});

// Add Controllers
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});
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
builder.Services.AddSingleton<ITotpService, TotpService>();
builder.Services.AddSingleton<IPasskeyService, PasskeyService>();
builder.Services.AddSingleton<IGoogleAuthService, GoogleAuthService>();
builder.Services.AddSingleton<IFirebaseAuthService, FirebaseAuthService>();
builder.Services.AddSingleton<ICsrfProtectionService, CsrfProtectionService>();
builder.Services.AddScoped<IAuditLoggingService, AuditLoggingService>();
builder.Services.AddScoped<IBanEnforcementService, BanEnforcementService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<ILicenseService, LicenseService>();
builder.Services.AddScoped<IReleaseService, ReleaseService>();
builder.Services.AddScoped<IContentAndPricingService, ContentAndPricingService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<ISupportService, SupportService>();
builder.Services.AddScoped<ISystemHealthService, SystemHealthService>();
builder.Services.AddScoped<IGeoPricingService, GeoPricingService>();
builder.Services.AddSingleton<IPaymentProviderFactory, PaymentProviderFactory>();
builder.Services.AddSingleton<IIpGeolocationService, HeaderAndRangeGeoLocationService>();
builder.Services.AddScoped<ISubscriptionEntitlementService, SubscriptionEntitlementService>();

// Configure Strict Environment-Specific CORS for Dashboard and Website
string[] allowedOrigins;
if (builder.Environment.IsDevelopment())
{
    allowedOrigins = new[]
    {
        "http://localhost",
        "http://localhost:80",
        "http://localhost:8080",
        "http://localhost:5000",
        "https://localhost:5001",
        "http://127.0.0.1",
        "http://127.0.0.1:80",
        "http://127.0.0.1:8080",
        "http://127.0.0.1:5500",
        "http://localhost:3000",
        "https://control.edm.local"
    };
}
else
{
    string? configuredOrigins = builder.Configuration["Cors:AllowedOrigins"] ?? Environment.GetEnvironmentVariable("EDM_CORS_ALLOWED_ORIGINS");
    if (!string.IsNullOrWhiteSpace(configuredOrigins))
    {
        allowedOrigins = configuredOrigins.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
    else
    {
        allowedOrigins = new[]
        {
            "https://control.edm-download.org",
            "https://edm-download.org"
        };
    }
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("DashboardCorsPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .WithHeaders("Content-Type", "Accept", "X-CSRF-Token", "X-XSRF-Token", "Authorization")
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Configure JWT Bearer Authentication (Header & Secure Cookie)
string? configuredJwtSecret = builder.Configuration["Jwt:SecretKey"] ?? Environment.GetEnvironmentVariable("EDM_JWT_SECRET");

if (builder.Environment.IsProduction())
{
    if (string.IsNullOrWhiteSpace(configuredJwtSecret) || 
        configuredJwtSecret.Equals("EDM_Development_Super_Secret_Key_For_Jwt_Signing_2026_Minimum_256_Bits!", StringComparison.Ordinal) ||
        configuredJwtSecret.Length < 32)
    {
        throw new InvalidOperationException("CRITICAL PRODUCTION SECURITY FAILURE: Production environment requires a valid secure JWT signing secret. Please set the EDM_JWT_SECRET environment variable or configure Jwt:SecretKey with a minimum of 256 bits.");
    }
}

string jwtSecret = !string.IsNullOrWhiteSpace(configuredJwtSecret) ? configuredJwtSecret : "EDM_Development_Super_Secret_Key_For_Jwt_Signing_2026_Minimum_256_Bits!";
string jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "EDM.ControlPlane";
string jwtAudience = builder.Configuration["Jwt:Audience"] ?? "EDM.Clients";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = builder.Environment.IsProduction();
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

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // First check Authorization header; if absent, read from secure HttpOnly cookie
            if (string.IsNullOrEmpty(context.Token) && context.Request.Cookies.TryGetValue("edm_admin_jwt", out var cookieToken))
            {
                context.Token = cookieToken;
            }
            return Task.CompletedTask;
        }
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

    // Login & auth rate limiter: max 10 requests per minute per IP in production (1000 in dev/test)
    int permitLimit = builder.Environment.IsDevelopment() || builder.Environment.EnvironmentName == "Testing" ? 1000 : 10;
    options.AddPolicy("AuthRateLimit", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = permitLimit,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));
});

var app = builder.Build();

// Ensure DB is initialized & Seed Initial Data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    var permService = scope.ServiceProvider.GetRequiredService<IPermissionService>();
    db.Database.EnsureCreated();

    // 1. Ensure Default Role Permissions
    permService.EnsureDefaultRolePermissionsAsync().GetAwaiter().GetResult();

    // 2. Seed Initial Super Admin if database has no users
    if (!db.Users.Any())
    {
        bool isDev = app.Environment.IsDevelopment();
        string? envPassword = Environment.GetEnvironmentVariable("EDM_SUPERADMIN_PASSWORD") ?? builder.Configuration["Admin:InitialPassword"];

        if (!isDev && string.IsNullOrWhiteSpace(envPassword))
        {
            // In Production, require explicit administrator provisioning via /api/v1/auth/setup-initial-admin
            Console.WriteLine("[SECURITY] Production DB initialized with 0 users. SuperAdmin must be provisioned via /api/v1/auth/setup-initial-admin or EDM_SUPERADMIN_PASSWORD.");
        }
        else
        {
            string initUsername = Environment.GetEnvironmentVariable("EDM_SUPERADMIN_USERNAME") 
                ?? builder.Configuration["Admin:InitialUsername"] 
                ?? "superadmin";
            string initEmail = Environment.GetEnvironmentVariable("EDM_SUPERADMIN_EMAIL") 
                ?? builder.Configuration["Admin:InitialEmail"] 
                ?? "admin@edm.local";
            string initPassword = !string.IsNullOrWhiteSpace(envPassword) 
                ? envPassword 
                : "Admin@EDM2026!SecureKey";

            var superAdmin = new User
            {
                Id = Guid.NewGuid(),
                Username = initUsername,
                Email = initEmail.ToLowerInvariant().Trim(),
                PasswordHash = hasher.HashPassword(initPassword),
                Role = UserRole.SUPER_ADMIN,
                IsActive = true,
                IsEmailVerified = true,
                TwoFactorEnabled = false,
                MustChangePassword = false,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            db.Users.Add(superAdmin);
            db.SaveChanges();
        }
    }

    // 3. Seed Default Commercial Plans
    if (!db.Plans.Any())
    {
        var freePlan = new Plan
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Code = "free",
            Name = "EDM Free Community",
            Tier = PlanTier.Free,
            Description = "Standard multi-stream download acceleration with essential features.",
            PriceMonthlyUsd = 0.00m,
            PriceYearlyUsd = 0.00m,
            MaxDevices = 1,
            MaxConcurrentDownloads = 3,
            FeaturesJson = "[\"Multi-stream 8-socket acceleration\",\"Browser extension integration\",\"Queue management\"]",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var proPlan = new Plan
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Code = "pro",
            Name = "EDM Pro Turbo",
            Tier = PlanTier.Pro,
            Description = "Full turbo 32-socket downloads, priority routing, automated media sniffing and cloud sync.",
            PriceMonthlyUsd = 4.99m,
            PriceYearlyUsd = 39.99m,
            MaxDevices = 5,
            MaxConcurrentDownloads = 10,
            FeaturesJson = "[\"32-socket turbo acceleration\",\"Smart dynamic bandwidth allocation\",\"4K/8K Media Grabber & Stream Sniffer\",\"Zero ads & priority VIP support\"]",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var entPlan = new Plan
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Code = "enterprise",
            Name = "EDM Enterprise Fleet",
            Tier = PlanTier.Enterprise,
            Description = "Unlimited high-speed concurrency, centralized policy deployment and API access.",
            PriceMonthlyUsd = 19.99m,
            PriceYearlyUsd = 199.99m,
            MaxDevices = 50,
            MaxConcurrentDownloads = 50,
            FeaturesJson = "[\"Unlimited concurrent stream sockets\",\"Centralized device fleet policy\",\"Custom corporate branding\",\"Dedicated 24/7 SLA support\"]",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Plans.AddRange(freePlan, proPlan, entPlan);
        db.SaveChanges();

        // Seed Pricing Tiers for Plans
        db.PricingTiers.AddRange(
            new PricingTier
            {
                Id = Guid.NewGuid(),
                PlanId = proPlan.Id,
                DisplayName = "Pro Monthly",
                MonthlyPrice = 4.99m,
                YearlyPrice = 4.99m * 12,
                Currency = "USD",
                FeaturesListJson = proPlan.FeaturesJson,
                BadgeText = null,
                IsHighlighted = false,
                SortOrder = 1,
                IsActive = true,
                UpdatedAtUtc = DateTime.UtcNow
            },
            new PricingTier
            {
                Id = Guid.NewGuid(),
                PlanId = proPlan.Id,
                DisplayName = "Pro Yearly (Save 33%)",
                MonthlyPrice = 3.33m,
                YearlyPrice = 39.99m,
                Currency = "USD",
                FeaturesListJson = proPlan.FeaturesJson,
                BadgeText = "Most Popular",
                IsHighlighted = true,
                SortOrder = 2,
                IsActive = true,
                UpdatedAtUtc = DateTime.UtcNow
            }
        );
        db.SaveChanges();
    }

    // 4. Seed Default Authoritative Release with Real SHA-256 and Size
    if (!db.Releases.Any(r => r.Platform == ClientType.DesktopWindows && !r.IsWithdrawn))
    {
        var rel = new Release
        {
            Id = Guid.NewGuid(),
            Platform = ClientType.DesktopWindows,
            Version = "2.1.0",
            Channel = "stable",
            MinimumSupportedVersion = "1.0.0",
            Title = "EDM 2.1.0 Turbo Release",
            ReleaseNotes = "High performance multi-stream 32-socket download engine.",
            PublishedAtUtc = DateTime.UtcNow,
            IsMandatory = false,
            IsPublished = true,
            Severity = ReleaseSeverity.Standard
        };

        var artifactId = Guid.NewGuid();
        rel.Artifacts.Add(new ReleaseArtifact
        {
            Id = artifactId,
            ReleaseId = rel.Id,
            ArtifactName = "EDM-Setup-v2.1.0.exe",
            Architecture = "x64",
            DownloadUrl = $"/api/v1/releases/artifacts/{artifactId}/download",
            Sha256Hash = "93049cf86301342dbdaae74256d4013a1e30133aa26a38dbe08e2a6e3e32d023",
            FileSizeBytes = 19807971
        });
        db.Releases.Add(rel);
        db.SaveChanges();
    }

    // 5. Seed Default Website Sections
    if (!db.WebsiteContents.Any())
    {
        db.WebsiteContents.AddRange(
            new WebsiteContent
            {
                Id = Guid.NewGuid(),
                SectionKey = "hero",
                Title = "The Fastest Download Manager on the Planet",
                ContentJson = "{\"subtitle\":\"Engineered in C# and Native Win32 for maximum socket efficiency and ultra-fast download acceleration.\",\"ctaText\":\"Download EDM Free\",\"ctaUrl\":\"/download\"}",
                Locale = "en",
                IsPublished = true,
                Version = 1,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            },
            new WebsiteContent
            {
                Id = Guid.NewGuid(),
                SectionKey = "features",
                Title = "Next-Generation Download Engine",
                ContentJson = "{\"features\":[{\"title\":\"32-Stream Parallel Multi-Socket Acceleration\",\"desc\":\"Splits large files dynamically for maximum bandwidth saturation.\"},{\"title\":\"Universal Browser Extensions\",\"desc\":\"Chrome, Edge, Firefox, Brave, and Opera native integration.\"}]}",
                Locale = "en",
                IsPublished = true,
                Version = 1,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            }
        );
        db.SaveChanges();
    }
}

// Middleware Pipeline
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseCors("DashboardCorsPolicy");

// 1. Serve Admin Dashboard at /edm-admin
string dashboardPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "EDM.ControlPlane.Dashboard"));
if (Directory.Exists(dashboardPath))
{
    var dashboardFileProvider = new PhysicalFileProvider(dashboardPath);

    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = dashboardFileProvider,
        RequestPath = "/edm-admin"
    });

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = dashboardFileProvider,
        RequestPath = "/edm-admin"
    });
}

// 2. Serve Public Website at root /
string websitePath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "website"));
if (Directory.Exists(websitePath))
{
    var websiteFileProvider = new PhysicalFileProvider(websitePath);

    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = websiteFileProvider,
        RequestPath = ""
    });

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = websiteFileProvider,
        RequestPath = ""
    });
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<BanEnforcementMiddleware>();
app.UseAuthorization();
app.UseMiddleware<CsrfProtectionMiddleware>();

// Map API Controllers
app.MapControllers();

// SPA Fallback Routing for /edm-admin and /
app.MapFallback(async context =>
{
    string path = context.Request.Path.Value ?? "";

    if (path.StartsWith("/edm-admin", StringComparison.OrdinalIgnoreCase))
    {
        string adminIndexPath = Path.Combine(dashboardPath, "index.html");
        if (File.Exists(adminIndexPath))
        {
            context.Response.ContentType = "text/html";
            await context.Response.SendFileAsync(adminIndexPath);
            return;
        }
    }

    string webIndexPath = Path.Combine(websitePath, "index.html");
    if (File.Exists(webIndexPath))
    {
        context.Response.ContentType = "text/html";
        await context.Response.SendFileAsync(webIndexPath);
        return;
    }

    context.Response.StatusCode = 404;
    await context.Response.WriteAsync("Not Found");
});

app.Run();

namespace EDM.ControlPlane.Api
{
    public partial class Program { }
}
