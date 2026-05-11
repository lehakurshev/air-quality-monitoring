using System.Data;
using Npgsql;

namespace AirQualityMonitoring.Infrastructure.Postgres;

public class NpgsqlDbConnectionFactory : IDbConnectionFactory
{
    private readonly NpgsqlDataSource _dataSource;

    public NpgsqlDbConnectionFactory(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        _dataSource = builder.Build();
    }

    public async Task<IDbConnection> CreateConnectionAsync(CancellationToken ct)
    {
        return await _dataSource.OpenConnectionAsync(ct);
    }
}

public interface IDbConnectionFactory
{
    Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken);
}