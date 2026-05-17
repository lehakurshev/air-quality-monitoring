using System.Text.Json;
using AirQualityMonitoring.Core.Interfaces;
using AirQualityMonitoring.Infrastructure.Postgres;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace AirQualityMonitoring.Core.Features.Measurements;

public sealed class GetAllEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder builder)
    {
        builder.MapGet(
            "/measurement",
            async ([FromServices] GetAllHandler handler, CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(cancellationToken);
                
                return Results.Json(result);
            }
        )
        .WithOpenApi(operation =>
        {
            operation.OperationId = "MeasurementGet";
            return operation;
        })
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .WithOpenApi();
    }
}

public sealed class GetAllHandler
{
    private readonly IDatabase _redis;

    public GetAllHandler(IConnectionMultiplexer redis)
    {
        _redis = redis.GetDatabase();
    }

    public async Task<List<JsonElement>> Handle(CancellationToken cancellationToken)
    {
        // получаем всех пользователей
        var userIds = await _redis.SetMembersAsync("air:users");

        if (userIds.Length == 0)
            return new List<JsonElement>();

        // ключи Redis
        var keys = userIds.Select(id => (RedisKey)$"air:user:{id}").ToArray();

        // читаем всё одним MGET
        var values = await _redis.StringGetAsync(keys);

        // десериализуем JSON обратно в JsonElement
        var result = values
            .Where(v => v.HasValue)
            .Select(v => JsonSerializer.Deserialize<JsonElement>(v.ToString()))
            .ToList();

        return result;
    }
}