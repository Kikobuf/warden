using System.ComponentModel.DataAnnotations;

namespace Warden.Web.Models;

/// <summary>
/// The lightweight record of every player ever seen, keyed by their game user ID.
/// Per spec, we only ever store a nickname and a user ID for a player — nothing else.
/// Nickname is updated whenever we see the player again (heartbeat or moderation action),
/// so this always reflects their most recently observed name.
/// </summary>
public class PlayerProfile
{
    /// <summary>The player's permanent in-game user ID. This is the primary key.</summary>
    [Key]
    [MaxLength(64)]
    public string UserId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string LatestNickname { get; set; } = string.Empty;

    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;

    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    public List<Ban> Bans { get; set; } = new();
    public List<Mute> Mutes { get; set; } = new();
    public List<Warning> Warnings { get; set; } = new();
    public List<KickRecord> Kicks { get; set; } = new();
}

public class Ban
{
    public int Id { get; set; }

    [Required]
    [MaxLength(64)]
    public string PlayerUserId { get; set; } = string.Empty;
    public PlayerProfile? Player { get; set; }

    /// <summary>Nickname at the time the ban was issued (kept even if the player later renames).</summary>
    [MaxLength(64)]
    public string PlayerNickname { get; set; } = string.Empty;

    [MaxLength(512)]
    public string Reason { get; set; } = string.Empty;

    /// <summary>Free-text identifier of whoever issued the ban (in-game admin nickname/ID, or web username).</summary>
    [MaxLength(128)]
    public string IssuedBy { get; set; } = string.Empty;

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Null means permanent.</summary>
    public DateTime? ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    [MaxLength(128)]
    public string? RevokedBy { get; set; }

    [MaxLength(512)]
    public string? RevokeReason { get; set; }

    public bool IsActive(DateTime utcNow) =>
        RevokedAt is null && (ExpiresAt is null || ExpiresAt > utcNow);
}

public class Mute
{
    public int Id { get; set; }

    [Required]
    [MaxLength(64)]
    public string PlayerUserId { get; set; } = string.Empty;
    public PlayerProfile? Player { get; set; }

    [MaxLength(64)]
    public string PlayerNickname { get; set; } = string.Empty;

    [MaxLength(512)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(128)]
    public string IssuedBy { get; set; } = string.Empty;

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Null means permanent.</summary>
    public DateTime? ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    [MaxLength(128)]
    public string? RevokedBy { get; set; }

    [MaxLength(512)]
    public string? RevokeReason { get; set; }

    public bool IsActive(DateTime utcNow) =>
        RevokedAt is null && (ExpiresAt is null || ExpiresAt > utcNow);
}

/// <summary>A warning is purely historical — it does not get "enforced" by the plugin.</summary>
public class Warning
{
    public int Id { get; set; }

    [Required]
    [MaxLength(64)]
    public string PlayerUserId { get; set; } = string.Empty;
    public PlayerProfile? Player { get; set; }

    [MaxLength(64)]
    public string PlayerNickname { get; set; } = string.Empty;

    [MaxLength(512)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(128)]
    public string IssuedBy { get; set; } = string.Empty;

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A kick record is purely historical, same as a warning — kicks aren't "active" states.</summary>
public class KickRecord
{
    public int Id { get; set; }

    [Required]
    [MaxLength(64)]
    public string PlayerUserId { get; set; } = string.Empty;
    public PlayerProfile? Player { get; set; }

    [MaxLength(64)]
    public string PlayerNickname { get; set; } = string.Empty;

    [MaxLength(512)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(128)]
    public string IssuedBy { get; set; } = string.Empty;

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Which server the kick happened on, if reported by a server rather than the web UI.</summary>
    public int? GameServerId { get; set; }
}
