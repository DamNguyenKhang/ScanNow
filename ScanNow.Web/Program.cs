using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using ScanNow.Application;
using ScanNow.Infrastructure;
using ScanNow.Web;
using ScanNow.Web.Configurations;
using ScanNow.Web.Middlewares;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Load .env files from both the web project and solution root.
// Local developers may run the API from either directory.
var envPaths = new[]
{
    Path.Combine(Directory.GetCurrentDirectory(), "..", ".env"),
    Path.Combine(AppContext.BaseDirectory, ".env"),
    Path.Combine(Directory.GetCurrentDirectory(), ".env")
}
    .Select(Path.GetFullPath)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .Where(File.Exists)
    .ToArray();

if (envPaths.Length > 0)
{
    foreach (var path in envPaths)
    {
        DotNetEnv.Env.Load(path);
    }
}
builder.Configuration.AddEnvironmentVariables();
var connectionString = builder.Configuration.GetConnectionString("ScanNowDB")
    ?? throw new InvalidOperationException("Connection string 'ScanNowDB' is not configured.");

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var allowedOrigins = new[]
{
    builder.Configuration["App:FrontendBaseUrl"],
    builder.Configuration["App:ClientUrl"],
    builder.Configuration["App:AllowedOrigins"],
    "http://localhost:5173",
    "http://localhost:3000",
    "https://carwash-magnifier-jogging.ngrok-free.dev",
    "http://localhost:3001"
}
    .Where(value => !string.IsNullOrWhiteSpace(value))
    .SelectMany(value => value!.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    .Select(origin => Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        ? uri.GetLeftPart(UriPartial.Authority)
        : origin.TrimEnd('/'))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToHashSet(StringComparer.OrdinalIgnoreCase);

// Domain used for multi-tenant subdomain routing (e.g. tenant1.scannow.site).
// Set App:ProductionDomain in appsettings / env to enable wildcard subdomain CORS.
var productionDomain = builder.Configuration["App:ProductionDomain"]; // e.g. "scannow.site"

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowClient", policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                // Allow explicit origins (localhost, configured URLs).
                if (allowedOrigins.Contains(origin))
                    return true;

                // Allow any subdomain of the configured production domain.
                // e.g. "https://tenant1.scannow.site" when productionDomain = "scannow.site"
                if (!string.IsNullOrWhiteSpace(productionDomain))
                {
                    if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                    {
                        var host = uri.Host; // "tenant1.scannow.site"
                        if (host.EndsWith($".{productionDomain}", StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }

                return false;
            })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromMinutes(15);
});

builder.Services.AddAuthorization();

builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddSignalR()
    .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services
    .AddDatabase(connectionString)
    .AddExceptionHandler()
    .AddPresentation()
    .AddApplication()
    .AddInfrastructure()
    .AddPayOS(builder.Configuration)
    ;

// Disable default claim type mapping so JWT claims are read as-is.
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

// Register JWT after Identity so Bearer remains the default auth scheme for API endpoints.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = builder.Configuration["Jwt:Issuer"],
                        ValidateAudience = true,
                        ValidAudience = builder.Configuration["Jwt:Audience"],
                        ValidateLifetime = true,
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SecretKey"]!)),
                        ValidateIssuerSigningKey = true,
                        RoleClaimType = "role",
                        NameClaimType = "name"
                    };
                    options.Events = new JwtEvents();
                });

builder.Services.Configure<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(options =>
{
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

if (!app.Environment.IsProduction())
    app.UseHttpsRedirection();

app.UseExceptionHandler();

app.UseCors("AllowClient");

// Resolve tenant from subdomain before authentication so that
// EF Core global query filters are active for the entire request.
app.UseMiddleware<TenantResolutionMiddleware>();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();
app.MapHub<ScanNow.Web.Hubs.CartHub>("/hubs/cart");
app.MapHub<ScanNow.Web.Hubs.OrderHub>("/hubs/orders");

// Auto migrate on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ScanNow.Infrastructure.ApplicationDbContext>();
    await db.Database.MigrateAsync();
}

// Seed roles and initial data
try
{
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        await ScanNow.Infrastructure.Data.DbContextSeeder.SeedAsync(services);
    }
}
catch (Exception ex)
{
    // Log seeding error to console; do not stop the app startup
    Console.WriteLine($"Warning: seeding failed - {ex.Message}");
}

app.Run();
