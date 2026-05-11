using System.Text.Json;
using AirQualityMonitoring.Core.Features.Auth;
using AirQualityMonitoring.Core.Interfaces;
using AirQualityMonitoring.Infrastructure.Postgres;
using ClassLibAirQualityMonitoring.Domain;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using Microsoft.AspNetCore.Authorization;

namespace AirQualityMonitoring.Core.Features.Measurements;

// Добавляем модель запроса
// Обновляем модель запроса
public record MeasurementRequest(
    double Co,
    double No2,
    double Pm25,
    double Pm10,
    double Latitude,
    double Longitude
);

public sealed class AddEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder builder)
    {
        builder.MapPost(
                "/measurement",
                async (
                    [FromBody] MeasurementRequest request, // Добавляем параметр
                    HttpContext context,
                    [FromServices] AddHandler handler,
                    CancellationToken cancellationToken) =>
                {
                    await handler.Handle(context, request, cancellationToken);
                    return Results.Ok(new { message = "Measurement added successfully" });
                })
            .RequireAuthorization()
            .WithName("AddMeasurement")
            .WithOpenApi();
    }
}

public sealed class AddHandler
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IDatabase _redis;
    private readonly TokenValidator _tokenValidator;

    public AddHandler(
        IDbConnectionFactory connectionFactory,
        IConnectionMultiplexer redis,
        TokenValidator tokenValidator)
    {
        _connectionFactory = connectionFactory;
        _redis = redis.GetDatabase();
        _tokenValidator = tokenValidator;
    }

    public async Task Handle(HttpContext context, MeasurementRequest request, CancellationToken cancellationToken)
    {
        // 1️⃣ Проверяем access token
        var userId = await _tokenValidator.ValidateAsync(context);

        if (userId == null)
            throw new UnauthorizedAccessException("Invalid token");

        // 2️⃣ Создаем JSON из запроса
        var pollutants = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            co = request.Co,
            no2 = request.No2,
            pm25 = request.Pm25,
            pm10 = request.Pm10,
            latitude = request.Latitude,
            longitude = request.Longitude,
            timestamp = DateTime.UtcNow
        }));

        var measurement = new Measurement
        {
            Id = Guid.NewGuid(),
            UserId = Guid.Parse(userId),
            Pollutants = pollutants
        };

        // 3️⃣ сохраняем в Postgres
        using var dbConnection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

        await dbConnection.ExecuteAsync(
        """
        insert into measurements (id, user_id, pollutants)
        values (@Id, @UserId, @Pollutants::jsonb)
        """,
        measurement);

        // 4️⃣ сохраняем последнее измерение в Redis
        var key = $"air:user:{measurement.UserId}";
        var json = JsonSerializer.Serialize(new
        {
            co = request.Co,
            no2 = request.No2,
            pm25 = request.Pm25,
            pm10 = request.Pm10,
            latitude = request.Latitude,
            longitude = request.Longitude,
            timestamp = DateTime.UtcNow
        });

        var batch = _redis.CreateBatch();

        batch.StringSetAsync(key, json);
        batch.SetAddAsync("air:users", measurement.UserId.ToString());

        batch.Execute();
    }
}