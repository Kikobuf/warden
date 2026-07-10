using System.ComponentModel.DataAnnotations;

namespace Warden.Web.Models.Dtos;

public class ModerationStatusResponse
{
    public bool IsBanned { get; set; }
    public string? BanReason { get; set; }
    public DateTime? BanExpiresAt { get; set; }
    public string? BannedBy { get; set; }

    public bool IsMuted { get; set; }
    public string? MuteReason { get; set; }
    public DateTime? MuteExpiresAt { get; set; }
    public string? MutedBy { get; set; }
}

public class IssueBanRequest
{
    [Required] public string UserId { get; set; } = string.Empty;
    [Required] public string Nickname { get; set; } = string.Empty;
    [Required] public string Reason { get; set; } = string.Empty;
    [Required] public string IssuedBy { get; set; } = string.Empty;

    /// <summary>Ban duration in seconds. Omit or set null for a permanent ban.</summary>
    public long? DurationSeconds { get; set; }
}

public class RevokeRequest
{
    [Required] public string UserId { get; set; } = string.Empty;
    [Required] public string RevokedBy { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public class IssueMuteRequest
{
    [Required] public string UserId { get; set; } = string.Empty;
    [Required] public string Nickname { get; set; } = string.Empty;
    [Required] public string Reason { get; set; } = string.Empty;
    [Required] public string IssuedBy { get; set; } = string.Empty;
    public long? DurationSeconds { get; set; }
}

public class IssueWarningRequest
{
    [Required] public string UserId { get; set; } = string.Empty;
    [Required] public string Nickname { get; set; } = string.Empty;
    [Required] public string Reason { get; set; } = string.Empty;
    [Required] public string IssuedBy { get; set; } = string.Empty;
}

public class LogKickRequest
{
    [Required] public string UserId { get; set; } = string.Empty;
    [Required] public string Nickname { get; set; } = string.Empty;
    [Required] public string Reason { get; set; } = string.Empty;
    [Required] public string IssuedBy { get; set; } = string.Empty;
}

public class PlayerHistoryResponse
{
    public string UserId { get; set; } = string.Empty;
    public string LatestNickname { get; set; } = string.Empty;
    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public List<BanDto> Bans { get; set; } = new();
    public List<MuteDto> Mutes { get; set; } = new();
    public List<WarningDto> Warnings { get; set; } = new();
    public List<KickDto> Kicks { get; set; } = new();
}

public class BanDto
{
    public int Id { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string IssuedBy { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedBy { get; set; }
    public bool IsActive { get; set; }
}

public class MuteDto
{
    public int Id { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string IssuedBy { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedBy { get; set; }
    public bool IsActive { get; set; }
}

public class WarningDto
{
    public int Id { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string IssuedBy { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; }
}

public class KickDto
{
    public int Id { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string IssuedBy { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; }
}
