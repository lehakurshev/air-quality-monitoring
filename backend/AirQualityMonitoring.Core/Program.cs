using AirQualityMonitoring.Core;
using AirQualityMonitoring.Core.Extensions;
using AirQualityMonitoring.Core.Features.Auth;
using AirQualityMonitoring.Infrastructure.Postgres;
using Dapper;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

builder.Services.AddEndpoints(typeof(Program).Assembly);

// Аутентификация
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "CustomScheme";
    options.DefaultChallengeScheme = "CustomScheme";
})
.AddScheme<AuthenticationSchemeOptions, CustomAuthenticationHandler>("CustomScheme", null);

builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Введите токен: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new List<string>()
        }
    });
});

builder.Services.AddSingleton<IDbConnectionFactory>(_ =>
    new NpgsqlDbConnectionFactory(
        builder.Configuration["DbConnectionString"]!
    )
);

// ✅ Redis (ПРАВИЛЬНО С USER + PASSWORD)
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();

    var host = config["Redis:Host"];
    var user = config["Redis:User"];
    var password = config["Redis:Password"];

    var options = new ConfigurationOptions
    {
        AbortOnConnectFail = false
    };

    options.EndPoints.Add(host);

    if (!string.IsNullOrEmpty(user))
        options.User = user;

    if (!string.IsNullOrEmpty(password))
        options.Password = password;

    return ConnectionMultiplexer.Connect(options);
});

builder.Services.AddSingleton<RedisManager>();
builder.Services.AddSingleton<TokenValidator>();

SqlMapper.AddTypeHandler(new JsonDocumentTypeHandler());

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.UseEndpoints(app.MapGroup("/api"));

app.Run();