using ClassLibAirQualityMonitoring.Domain;
using StackExchange.Redis;

namespace AirQualityMonitoring.Core.Features.Auth;

public class RedisManager
{
    private readonly IDatabase _redisDb;

    public RedisManager(IConnectionMultiplexer redis)
    {
        _redisDb = redis.GetDatabase();
    }

    public async Task StoreTokenAsync(TokenModel model)
    {
        string redisKey = model.AccessToken;

        var entries = new HashEntry[]
        {
            new("accesstoken", model.AccessToken),
            new("accesstokenexpiry", model.AccessTokenExpiry.ToString("yyyy-MM-dd HH:mm:ss")),
            new("refreshtoken", model.RefreshToken),
            new("refreshtokenexpiry", model.RefreshTokenExpiry.ToString("yyyy-MM-dd HH:mm:ss")),
            new("userid", model.UserId),
            new("lastupdate", model.LastUpdate.ToString("yyyy-MM-dd HH:mm:ss"))
        };

        await _redisDb.HashSetAsync(redisKey, entries);
        await _redisDb.KeyExpireAsync(redisKey, model.RefreshTokenExpiry);
    }

    public async Task<TokenModel?> GetTokenAsync(string accessToken)
    {
        var entries = await _redisDb.HashGetAllAsync(accessToken);
        if (entries.Length == 0) return null;

        var dict = entries.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());

        return new TokenModel
        {
            AccessToken = dict["accesstoken"],
            AccessTokenExpiry = Convert.ToDateTime(dict["accesstokenexpiry"]),
            RefreshToken = dict["refreshtoken"],
            RefreshTokenExpiry = Convert.ToDateTime(dict["refreshtokenexpiry"]),
            UserId = dict["userid"],
            LastUpdate = Convert.ToDateTime(dict["lastupdate"])
        };
    }

    public async Task<bool> RemoveTokenAsync(string accessToken)
    {
        return await _redisDb.KeyDeleteAsync(accessToken);
    }

    public async Task<bool> TokenExistsAsync(string accessToken)
    {
        return await _redisDb.KeyExistsAsync(accessToken);
    }

    public async Task<string?> GetUserIdFromTokenAsync(string accessToken)
    {
        return await _redisDb.HashGetAsync(accessToken, "userid");
    }
}