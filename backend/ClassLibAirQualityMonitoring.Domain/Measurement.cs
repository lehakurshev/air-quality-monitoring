namespace ClassLibAirQualityMonitoring.Domain;

using System.Text.Json;

public sealed class Measurement
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public JsonDocument Pollutants { get; set; }
}