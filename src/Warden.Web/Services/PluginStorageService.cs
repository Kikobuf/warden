using System.Security.Cryptography;

namespace Warden.Web.Services;

/// <summary>
/// Reads and writes plugin DLL files on disk, outside wwwroot, so they can never be served
/// as static files — the only way to fetch one is through the authenticated download endpoint.
/// </summary>
public class PluginStorageService
{
    private readonly string _storagePath;

    public PluginStorageService(IConfiguration configuration, IWebHostEnvironment env)
    {
        var configuredPath = configuration["Warden:PluginStoragePath"] ?? "App_Data/PluginStorage";
        _storagePath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(env.ContentRootPath, configuredPath);

        Directory.CreateDirectory(_storagePath);
    }

    /// <summary>Saves an uploaded stream under a new random file name and returns (storedFileName, sha256Hash, sizeBytes).</summary>
    public async Task<(string StoredFileName, string Sha256Hash, long SizeBytes)> SaveAsync(Stream content, CancellationToken ct = default)
    {
        var storedFileName = $"{Guid.NewGuid():N}.dll";
        var fullPath = Path.Combine(_storagePath, storedFileName);

        using var sha256 = SHA256.Create();
        await using (var fileStream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write))
        await using (var hashingStream = new CryptoStream(fileStream, sha256, CryptoStreamMode.Write))
        {
            await content.CopyToAsync(hashingStream, ct);
        }

        var hash = Convert.ToHexString(sha256.Hash ?? Array.Empty<byte>());
        var size = new FileInfo(fullPath).Length;
        return (storedFileName, hash, size);
    }

    public Stream OpenRead(string storedFileName)
    {
        var fullPath = Path.Combine(_storagePath, storedFileName);
        return new FileStream(fullPath, FileMode.Open, FileAccess.Read);
    }

    public void Delete(string storedFileName)
    {
        var fullPath = Path.Combine(_storagePath, storedFileName);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }
}
