using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using ScanNow.Application;
using ScanNow.Infrastructure;
using ScanNow.Web;
using ScanNow.Web.Configurations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Load .env from project directory first, then solution root as fallback
var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (!File.Exists(envPath))
    envPath = Path.Combine(AppContext.BaseDirectory, ".env");
DotNetEnv.Env.Load(envPath);
builder.Configuration.AddEnvironmentVariables();
var connectionString = builder.Configuration.GetConnectionString("ScanNowDB")
    ?? throw new InvalidOperationException("Connection string 'ScanNowDB' is not configured.");

builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowClient",
        policy => policy.WithOrigins(builder.Configuration["App:FrontendBaseUrl"] ?? "http://localhost:5173")
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials());
});

builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromMinutes(15);
});

builder.Services.AddAuthorization();

builder.Services.AddHttpContextAccessor();

builder.Services
    .AddDatabase(connectionString)
    .AddExceptionHandler()
    .AddPresentation()
    .AddApplication()
    .AddInfrastructure()
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

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

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
