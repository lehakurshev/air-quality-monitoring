using AirQualityMonitoring.Core;
using AirQualityMonitoring.Core.Extensions;
using AirQualityMonitoring.Core.Features.Auth;
using AirQualityMonitoring.Infrastructure.Postgres;
using Dapper;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;
using Microsoft.AspNetCore.Authentication; // Добавьте

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpoints(typeof(Program).Assembly);

// Исправленная аутентификация
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
    new NpgsqlDbConnectionFactory(builder.Configuration["DbConnectionString"]!));

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return ConnectionMultiplexer.Connect(new ConfigurationOptions
    {
        EndPoints = { config["Redis:Host"] },
        Password = config["Redis:Password"],
        AbortOnConnectFail = false
    });
});

builder.Services.AddSingleton<RedisManager>();
builder.Services.AddSingleton<TokenValidator>();

SqlMapper.AddTypeHandler(new JsonDocumentTypeHandler());

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();  // Должен быть перед UseAuthorization
app.UseAuthorization();

app.UseEndpoints(app.MapGroup("/api"));

app.Run();