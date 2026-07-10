using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Warden.Web.Data;

namespace Warden.Web.Services;

public static class ServerApiKeyDefaults
{
    public const string AuthenticationScheme = "ServerApiKey";
    public const string HeaderName = "X-Server-Api-Key";

    /// <summary>Claim type carrying the authenticated GameServer's database ID.</summary>
    public const string ServerIdClaim = "warden:server_id";

    /// <summary>Claim type carrying the authenticated GameServer's name.</summary>
    public const string ServerNameClaim = "warden:server_name";
}

public class ServerApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
}

/// <summary>
/// Authenticates requests from game servers using the "X-Server-Api-Key" header.
/// The plaintext key is hashed and compared against the stored hash for each GameServer —
/// plaintext keys are never persisted, so this is the only way to authenticate them.
/// </summary>
public class ServerApiKeyAuthenticationHandler : AuthenticationHandler<ServerApiKeyAuthenticationOptions>
{
    private readonly WardenDbContext _db;

    public ServerApiKeyAuthenticationHandler(
        IOptionsMonitor<ServerApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        WardenDbContext db)
        : base(options, logger, encoder)
    {
        _db = db;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ServerApiKeyDefaults.HeaderName, out var headerValues))
        {
            return AuthenticateResult.Fail($"Missing {ServerApiKeyDefaults.HeaderName} header.");
        }

        var plaintextKey = headerValues.ToString();
        if (string.IsNullOrWhiteSpace(plaintextKey))
        {
            return AuthenticateResult.Fail("Empty API key.");
        }

        var hash = ApiKeyService.Hash(plaintextKey);

        var server = await _db.GameServers.FirstOrDefaultAsync(s => s.ApiKeyHash == hash);
        if (server is null)
        {
            return AuthenticateResult.Fail("Invalid API key.");
        }

        var claims = new[]
        {
            new Claim(ServerApiKeyDefaults.ServerIdClaim, server.Id.ToString()),
            new Claim(ServerApiKeyDefaults.ServerNameClaim, server.Name),
        };
        var identity = new ClaimsIdentity(claims, ServerApiKeyDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, ServerApiKeyDefaults.AuthenticationScheme);

        return AuthenticateResult.Success(ticket);
    }
}
