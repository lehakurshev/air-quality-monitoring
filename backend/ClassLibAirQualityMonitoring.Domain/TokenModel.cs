namespace ClassLibAirQualityMonitoring.Domain;

public class TokenModel
{
    public string? AccessToken { get; set; }
    public DateTime AccessTokenExpiry { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime RefreshTokenExpiry { get; set; }
    public string? UserId { get; set; }
    public DateTime LastUpdate { get; set; }
}