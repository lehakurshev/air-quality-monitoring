using System.Globalization;
using System.Reflection;
using AirQualityMonitoring.Core;
using AirQualityMonitoring.Core.Extensions;
using AirQualityMonitoring.Core.Features.Auth;
using AirQualityMonitoring.Core.Swagger;
using AirQualityMonitoring.Infrastructure.Postgres;
using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;
using Swashbuckle.AspNetCore.SwaggerUI;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

// =====================================
// Core services
// =====================================
builder.Services.AddEndpoints(typeof(Program).Assembly);

builder.Services.AddLocalization(options =>
{
    options.ResourcesPath = "Resources";
});

builder.Services.AddEndpointsApiExplorer();

// Swagger
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("ru", new OpenApiInfo
    {
        Version = "ru"
    });

    options.SwaggerDoc("en", new OpenApiInfo
    {
        Version = "en"
    });

    options.DocumentFilter<SwaggerLocalizationDocumentFilter>();

    options.EnableAnnotations();

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = """
        Bearer token authentication.

        Example:
        Bearer eyJhbGc...
        """,
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
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
            Array.Empty<string>()
        }
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

    options.IncludeXmlComments(xmlPath, true);
});

// =====================================
// Auth
// =====================================
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "CustomScheme";
    options.DefaultChallengeScheme = "CustomScheme";
})
.AddScheme<AuthenticationSchemeOptions, CustomAuthenticationHandler>(
    "CustomScheme",
    null);

builder.Services.AddAuthorization();

// =====================================
// Infrastructure
// =====================================
builder.Services.AddSingleton<IDbConnectionFactory>(_ =>
    new NpgsqlDbConnectionFactory(
        builder.Configuration["DbConnectionString"]!
    )
);

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();

    var options = new ConfigurationOptions
    {
        AbortOnConnectFail = false
    };

    options.EndPoints.Add(config["Redis:Host"]);
    options.User = config["Redis:User"];
    options.Password = config["Redis:Password"];

    return ConnectionMultiplexer.Connect(options);
});

builder.Services.AddSingleton<RedisManager>();
builder.Services.AddSingleton<TokenValidator>();

SqlMapper.AddTypeHandler(new JsonDocumentTypeHandler());

var app = builder.Build();


// =====================================
// IMPORTANT: NO culture switching here
// =====================================
app.UseSwagger();

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/ru/swagger.json", "RU");
    options.SwaggerEndpoint("/swagger/en/swagger.json", "EN");

    options.RoutePrefix = "swagger";
});

// =====================================
// Auth middleware
// =====================================
app.UseAuthentication();
app.UseAuthorization();

// =====================================
// Endpoints
// =====================================
app.UseEndpoints(app.MapGroup("/api"));

app.Run();