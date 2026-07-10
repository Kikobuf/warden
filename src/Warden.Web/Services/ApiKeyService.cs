using System.Security.Cryptography;
using System.Text;

namespace Warden.Web.Services;

/// <summary>
/// Generates and hashes API keys used by game servers to authenticate against the
/// server-facing APIs (moderation, heartbeat, plugin download). Keys are shown in
/// plaintext exactly once (at creation/regeneration); only the SHA-256 hash is stored.
/// </summary>
public static class ApiKeyService
{
    private const string Prefix = "wsk"; // "Warden Server Key"

    /// <summary>Creates a new cryptographically random plaintext API key.</summary>
    public static string GenerateKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(bytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "");
        return $"{Prefix}_{token}";
    }

    public static string Hash(string plaintextKey)
    {
        var bytes = Encoding.UTF8.GetBytes(plaintextKey);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    /// <summary>Short, non-secret prefix of the key, safe to display in the UI for identification.</summary>
    public static string DisplayPrefix(string plaintextKey) =>
        plaintextKey.Length <= 12 ? plaintextKey : plaintextKey[..12] + "…";
}
