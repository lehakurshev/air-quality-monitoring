using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AirQualityMonitoring.Core.Features.Auth;

public class CustomAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly TokenValidator _tokenValidator;

    public CustomAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        TokenValidator tokenValidator)
        : base(options, logger, encoder, clock)
    {
        _tokenValidator = tokenValidator;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Проверяем заголовок Authorization
        if (!Request.Headers.ContainsKey("Authorization"))
            return AuthenticateResult.NoResult();

        string authorization = Request.Headers["Authorization"];

        if (string.IsNullOrEmpty(authorization))
            return AuthenticateResult.NoResult();

        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var token = authorization.Substring("Bearer ".Length).Trim();

        // Валидируем токен
        var userId = await _tokenValidator.ValidateTokenAsync(token);
        
        if (string.IsNullOrEmpty(userId))
            return AuthenticateResult.Fail("Invalid token");

        // Создаем claims
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, userId)
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}