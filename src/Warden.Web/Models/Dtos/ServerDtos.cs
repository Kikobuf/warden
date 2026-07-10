using System.ComponentModel.DataAnnotations;

namespace Warden.Web.Models.Dtos;

public class HeartbeatRequest
{
    /// <summary>Current ticks-per-second of the server.</summary>
    [Required]
    public double Tps { get; set; }

    public List<HeartbeatPlayer> Players { get; set; } = new();
}

public class HeartbeatPlayer
{
    [Required] public string UserId { get; set; } = string.Empty;
    [Required] public string Nickname { get; set; } = string.Empty;
}

public class HeartbeatResponse
{
    public string ServerName { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
}

public class PluginManifestEntry
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Sha256Hash { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
}
