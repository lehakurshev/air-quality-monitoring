namespace ClassLibAirQualityMonitoring.Domain;

public sealed class UserRow
{
    public Guid id { get; set; }
    public string email { get; set; } = default!;
    public string password_hash { get; set; } = default!;
    public string api_token { get; set; } = default!;
}