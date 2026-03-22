using StackExchange.Redis;
using Microsoft.AspNetCore.Http;

namespace AirQualityMonitoring.Core.Features.Auth;

public class TokenValidator
{
    private readonly IDatabase _redis;

    public TokenValidator(IConnectionMultiplexer redis)
    {
        _redis = redis.GetDatabase();
    }

    public async Task<string?> ValidateAsync(HttpContext context)
    {
        var authHeader = context.Request.Headers["Authorization"].ToString();
        
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            return null;

        var accessToken = authHeader["Bearer ".Length..];
        
        return await ValidateTokenAsync(accessToken);
    }

    public async Task<string?> ValidateTokenAsync(string accessToken)
    {
        // Ищем токен в Redis
        var userId = await _redis.StringGetAsync($"access:{accessToken}");
        
        return userId.HasValue ? userId.ToString() : null;
    }
}