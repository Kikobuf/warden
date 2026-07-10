using System.ComponentModel.DataAnnotations;

namespace Warden.Web.Models;

/// <summary>A game server registered with Warden. Identified to the API by its hashed API key.</summary>
public class GameServer
{
    public int Id { get; set; }

    [Required]
    [MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? Description { get; set; }

    /// <summary>SHA-256 hash of the API key. The plaintext key is only ever shown once, at creation/regeneration.</summary>
    [Required]
    [MaxLength(128)]
    public string ApiKeyHash { get; set; } = string.Empty;

    /// <summary>First 8 characters of the plaintext key, kept so admins can visually identify keys in the UI/logs.</summary>
    [MaxLength(16)]
    public string ApiKeyPrefix { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastHeartbeatAt { get; set; }

    public double? LastTps { get; set; }

    public int LastPlayerCount { get; set; }

    /// <summary>Currently connected players as of the last heartbeat.</summary>
    public List<ServerPlayerSession> PlayerSessions { get; set; } = new();

    public List<PluginAssignment> PluginAssignments { get; set; } = new();

    public bool IsOnline(DateTime utcNow, int offlineThresholdSeconds) =>
        LastHeartbeatAt is not null && (utcNow - LastHeartbeatAt.Value).TotalSeconds <= offlineThresholdSeconds;
}

/// <summary>A player currently known to be connected to a specific server (replaced wholesale each heartbeat).</summary>
public class ServerPlayerSession
{
    public int Id { get; set; }

    public int GameServerId { get; set; }
    public GameServer? GameServer { get; set; }

    [MaxLength(64)]
    public string PlayerUserId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string PlayerNickname { get; set; } = string.Empty;

    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
}

/// <summary>An uploaded plugin DLL that servers can be assigned to fetch on startup.</summary>
public class Plugin
{
    public int Id { get; set; }

    [Required]
    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(32)]
    public string Version { get; set; } = "1.0.0";

    [MaxLength(256)]
    public string? Description { get; set; }

    /// <summary>Name of the file as stored on disk under the plugin storage path (not user-controlled).</summary>
    [Required]
    [MaxLength(128)]
    public string StoredFileName { get; set; } = string.Empty;

    /// <summary>Original file name as uploaded, for display and for the filename servers download it as.</summary>
    [MaxLength(256)]
    public string OriginalFileName { get; set; } = string.Empty;

    [MaxLength(64)]
    public string Sha256Hash { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(128)]
    public string UploadedBy { get; set; } = string.Empty;

    /// <summary>If true, this plugin is delivered to every server, including ones created after upload.</summary>
    public bool IsGlobal { get; set; }

    public List<PluginAssignment> Assignments { get; set; } = new();
}

/// <summary>Explicit assignment of a (non-global) plugin to a specific server.</summary>
public class PluginAssignment
{
    public int Id { get; set; }

    public int PluginId { get; set; }
    public Plugin Plugin { get; set; } = null!;

    public int GameServerId { get; set; }
    public GameServer GameServer { get; set; } = null!;
}
