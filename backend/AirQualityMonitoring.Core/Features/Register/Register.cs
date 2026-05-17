using AirQualityMonitoring.Core.Interfaces;
using AirQualityMonitoring.Core.Swagger;
using AirQualityMonitoring.Infrastructure.Postgres;
using ClassLibAirQualityMonitoring.Domain;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace AirQualityMonitoring.Core.Features.Register;


public sealed class RegisterEndpoint : IEndpoint
{
        private readonly IStringLocalizerFactory _factory;

        public RegisterEndpoint(IStringLocalizerFactory factory)
        {
            _factory = factory;
        }
    
    public void MapEndpoint(IEndpointRouteBuilder builder)
    {
        builder.MapPost(
                "/auth/register",
                async (
                    [FromBody] RegisterRequest req,
                    [FromServices] RegisterHandler handler,
                    CancellationToken ct) =>
                {
                    var token =
                        await handler.Handle(
                            req.Email,
                            req.Password,
                            ct);

                    return Results.Ok(new
                    {
                        apiToken = token
                    });
                })
            
            .WithOpenApi(operation =>
            {
                operation.OperationId = "Register";
                return operation;
            })
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithOpenApi();
    }
}

public sealed class RegisterHandler
{
    private readonly IDbConnectionFactory _db;

    public RegisterHandler(IDbConnectionFactory db)
    {
        _db = db;
    }

    public async Task<string> Handle(
    string email,
    string password,
    CancellationToken ct)
    {
        using var connection = await _db.CreateConnectionAsync(ct);

        // =====================================
        // Existing user
        // =====================================

        var user = await connection.QueryFirstOrDefaultAsync<UserRow>(
            """
            select id, email, password_hash, api_token
            from users
            where email = @Email
            """,
            new { Email = email });

        if (user != null)
        {
            var passwordValid =
                BCrypt.Net.BCrypt.Verify(
                    password,
                    user.password_hash);

            if (!passwordValid)
                throw new Exception("Invalid password");

            return user.api_token;
        }

        // =====================================
        // Create new user
        // =====================================

        var userId = Guid.NewGuid();

        var apiToken = Guid.NewGuid().ToString();

        var passwordHash =
            BCrypt.Net.BCrypt.HashPassword(
                password,
                workFactor: 6);

        try
        {
            await connection.ExecuteAsync(
                """
                insert into users
                (
                    id,
                    email,
                    password_hash,
                    api_token,
                    created_at
                )
                values
                (
                    @Id,
                    @Email,
                    @PasswordHash,
                    @ApiToken,
                    now()
                )
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
        catch (Npgsql.PostgresException ex)
            when (ex.SqlState == "23505")
        {
            // =====================================
            // User was created concurrently
            // =====================================

            var existingUser =
                await connection.QuerySingleAsync<UserRow>(
                    """
                    select id, email, password_hash, api_token
                    from users
                    where email = @Email
                    """,
                    new { Email = email });

            var passwordValid =
                BCrypt.Net.BCrypt.Verify(
                    password,
                    existingUser.password_hash);

            if (!passwordValid)
                throw new Exception("Invalid password");

            return existingUser.api_token;
        }
    }
}

public record RegisterRequest(string Email, string Password);