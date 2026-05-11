using AirQualityMonitoring.Core.Interfaces;
using AirQualityMonitoring.Infrastructure.Postgres;
using ClassLibAirQualityMonitoring.Domain;
using Dapper;
using Microsoft.AspNetCore.Mvc;

namespace AirQualityMonitoring.Core.Features.Register;

public sealed class RegisterEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder builder)
    {
        builder.MapPost("/auth/register",
            async ([FromBody] RegisterRequest req,
                [FromServices] RegisterHandler handler,
                CancellationToken ct) =>
            {
                var token = await handler.Handle(req.Email, req.Password, ct);
                return Results.Ok(new { apiToken = token });
            });
    }
}

public sealed class RegisterHandler
{
    private readonly IDbConnectionFactory _db;

    public RegisterHandler(IDbConnectionFactory db)
    {
        _db = db;
    }

    public async Task<string> Handle(string email, string password, CancellationToken ct)
    {
        using var connection = await _db.CreateConnectionAsync(ct);

        var user = await connection.QueryFirstOrDefaultAsync<UserRow>(
            """
            select id, email, password_hash, api_token
            from users
            where email = @Email
            """,
            new { Email = email });

        if (user != null)
        {
            var passwordValid = BCrypt.Net.BCrypt.Verify(password, user.password_hash);

            if (!passwordValid)
                throw new Exception("Invalid password");

            return user.api_token;
        }

        var userId = Guid.NewGuid();
        var apiToken = Guid.NewGuid().ToString();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 6);

        await connection.ExecuteAsync(
            """
            insert into users (id,email,password_hash,api_token,created_at)
            values (@Id,@Email,@PasswordHash,@ApiToken,now())
            """,
            new
            {
                Id = userId,
                Email = email,
                PasswordHash = passwordHash,
                ApiToken = apiToken
            });

        return apiToken;
    }
}

public record RegisterRequest(string Email, string Password);