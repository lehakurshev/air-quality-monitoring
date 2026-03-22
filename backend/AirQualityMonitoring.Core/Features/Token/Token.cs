using AirQualityMonitoring.Core.Features.Auth;
using AirQualityMonitoring.Core.Interfaces;
using AirQualityMonitoring.Infrastructure.Postgres;
using ClassLibAirQualityMonitoring.Domain;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace AirQualityMonitoring.Core.Features.Token;

public sealed class TokenEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder builder)
    {
        builder.MapPost("/auth/token",
            async ([FromBody] TokenRequest req,
                [FromServices] CreateAccessTokenHandler handler,
                CancellationToken ct) =>
            {
                var token = await handler.Handle(req.ApiToken, ct);

                return token == null
                    ? Results.Unauthorized()
                    : Results.Ok(token);
            });
    }
}

public sealed class CreateAccessTokenHandler
{
    private readonly IDbConnectionFactory _db;
    private readonly IConnectionMultiplexer _redis;

    public CreateAccessTokenHandler(IDbConnectionFactory db, IConnectionMultiplexer redis)
    {
        _db = db;
        _redis = redis;
    }

    public async Task<TokenModel?> Handle(string apiToken, CancellationToken ct)
    {
        using var connection = await _db.CreateConnectionAsync(ct);

        var userId = await connection.ExecuteScalarAsync<Guid?>(
            """
            select id
            from users
            where api_token = @ApiToken
            """,
            new { ApiToken = apiToken });

        if (userId == null)
            return null;

        var token = new TokenModel
        {
            AccessToken = Guid.NewGuid().ToString(),
            RefreshToken = Guid.NewGuid().ToString(),
            UserId = userId.ToString(),
            AccessTokenExpiry = DateTime.UtcNow.AddMinutes(30),
            RefreshTokenExpiry = DateTime.UtcNow.AddDays(7),
            LastUpdate = DateTime.UtcNow
        };

        // Сохраняем токен в Redis с правильным ключом
        var db = _redis.GetDatabase();
        await db.StringSetAsync(
            $"access:{token.AccessToken}", 
            token.UserId, 
            TimeSpan.FromMinutes(30));

        return token;
    }
}

public record TokenRequest(string ApiToken);