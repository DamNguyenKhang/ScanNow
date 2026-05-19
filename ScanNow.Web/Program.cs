using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using ScanNow.Application;
using ScanNow.Infrastructure;
using ScanNow.Web;
using ScanNow.Web.Configurations;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

DotNetEnv.Env.Load();
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

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = builder.Configuration["Jwt:Issuer"],
                        ValidateAudience = true,
                        ValidAudience = builder.Configuration["Jwt:Audience"],
                        ValidateLifetime = true,
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SecretKey"]!)),
                        ValidateIssuerSigningKey = true
                    };
                    options.Events = new JwtEvents();
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

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseExceptionHandler();

app.UseCors("AllowClient");

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();
