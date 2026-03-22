using System.Data;
using System.Text.Json;
using Dapper;
using Npgsql;
using NpgsqlTypes;
// Добавьте этот using для NpgsqlParameter

// Добавьте этот using для NpgsqlDbType

// Этот обработчик будет для Npgsql (PostgreSQL)
namespace AirQualityMonitoring.Core;

public class JsonDocumentTypeHandler : SqlMapper.TypeHandler<JsonDocument>
{
    public override void SetValue(IDbDataParameter parameter, JsonDocument? value)
    {
        parameter.Value = value?.RootElement.GetRawText(); 

        if (parameter is NpgsqlParameter npgsqlParameter)
        {
            npgsqlParameter.NpgsqlDbType = NpgsqlDbType.Jsonb;
        }
    }

    public override JsonDocument? Parse(object? value)
    {
        return value is null or DBNull ? null : JsonDocument.Parse(value.ToString() ?? string.Empty);
    }
}