using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Warden.Web.Data;
using Warden.Web.Models;
using Warden.Web.Models.Dtos;
using Warden.Web.Services;

namespace Warden.Web.Controllers.Api;

/// <summary>
/// Server-to-server moderation API. Authenticated via the "X-Server-Api-Key" header
/// (see <see cref="ServerApiKeyAuthenticationHandler"/>), not by a web login — this is what
/// the in-game plugin calls to enforce bans/mutes and to record warnings/kicks/history.
///
/// All endpoints are namespaced under /api/v1/moderation. See docs/API.md for full request
/// and response examples.
/// </summary>
[ApiController]
[Route("api/v1/moderation")]
[Authorize(AuthenticationSchemes = ServerApiKeyDefaults.AuthenticationScheme)]
public class ModerationApiController : ControllerBase
{
    private readonly ModerationService _moderation;
    private readonly WardenDbContext _db;

    public ModerationApiController(ModerationService moderation, WardenDbContext db)
    {
        _moderation = moderation;
        _db = db;
    }

    private string ServerName => User.FindFirst(ServerApiKeyDefaults.ServerNameClaim)?.Value ?? "unknown-server";

    /// <summary>Checks whether a player is currently banned and/or muted. Call this on join and periodically.</summary>
    [HttpGet("status/{userId}")]
    public async Task<ActionResult<ModerationStatusResponse>> GetStatus(string userId)
    {
        var status = await _moderation.GetStatusAsync(userId);
        return Ok(new ModerationStatusResponse
        {
            IsBanned = status.IsBanned,
            BanReason = status.BanReason,
            BanExpiresAt = status.BanExpiresAt,
            BannedBy = status.BannedBy,
            IsMuted = status.IsMuted,
            MuteReason = status.MuteReason,
            MuteExpiresAt = status.MuteExpiresAt,
            MutedBy = status.MutedBy,
        });
    }

    [HttpPost("ban")]
    public async Task<ActionResult<BanDto>> IssueBan(IssueBanRequest request)
    {
        var duration = request.DurationSeconds is > 0 ? TimeSpan.FromSeconds(request.DurationSeconds.Value) : (TimeSpan?)null;
        var ban = await _moderation.IssueBanAsync(request.UserId, request.Nickname, request.Reason, request.IssuedBy, duration);
        await LogAsync("ban.issue", $"{request.IssuedBy} banned {request.Nickname} ({request.UserId}): {request.Reason}");

        return Ok(new BanDto
        {
            Id = ban.Id, Reason = ban.Reason, IssuedBy = ban.IssuedBy, IssuedAt = ban.IssuedAt,
            ExpiresAt = ban.ExpiresAt, IsActive = ban.IsActive(DateTime.UtcNow),
        });
    }

    [HttpPost("unban")]
    public async Task<IActionResult> RevokeBan(RevokeRequest request)
    {
        var revoked = await _moderation.RevokeBanAsync(request.UserId, request.RevokedBy, request.Reason);
        if (!revoked) return NotFound(new { message = "No active ban found for this user." });

        await LogAsync("ban.revoke", $"{request.RevokedBy} unbanned {request.UserId}");
        return NoContent();
    }

    [HttpPost("mute")]
    public async Task<ActionResult<MuteDto>> IssueMute(IssueMuteRequest request)
    {
        var duration = request.DurationSeconds is > 0 ? TimeSpan.FromSeconds(request.DurationSeconds.Value) : (TimeSpan?)null;
        var mute = await _moderation.IssueMuteAsync(request.UserId, request.Nickname, request.Reason, request.IssuedBy, duration);
        await LogAsync("mute.issue", $"{request.IssuedBy} muted {request.Nickname} ({request.UserId}): {request.Reason}");

        return Ok(new MuteDto
        {
            Id = mute.Id, Reason = mute.Reason, IssuedBy = mute.IssuedBy, IssuedAt = mute.IssuedAt,
            ExpiresAt = mute.ExpiresAt, IsActive = mute.IsActive(DateTime.UtcNow),
        });
    }

    [HttpPost("unmute")]
    public async Task<IActionResult> RevokeMute(RevokeRequest request)
    {
        var revoked = await _moderation.RevokeMuteAsync(request.UserId, request.RevokedBy, request.Reason);
        if (!revoked) return NotFound(new { message = "No active mute found for this user." });

        await LogAsync("mute.revoke", $"{request.RevokedBy} unmuted {request.UserId}");
        return NoContent();
    }

    [HttpPost("warn")]
    public async Task<ActionResult<WarningDto>> IssueWarning(IssueWarningRequest request)
    {
        var warning = await _moderation.IssueWarningAsync(request.UserId, request.Nickname, request.Reason, request.IssuedBy);
        await LogAsync("warning.issue", $"{request.IssuedBy} warned {request.Nickname} ({request.UserId}): {request.Reason}");

        return Ok(new WarningDto { Id = warning.Id, Reason = warning.Reason, IssuedBy = warning.IssuedBy, IssuedAt = warning.IssuedAt });
    }

    /// <summary>Purely historical — the plugin performs the actual kick itself; this just records it.</summary>
    [HttpPost("kick")]
    public async Task<ActionResult<KickDto>> LogKick(LogKickRequest request)
    {
        var serverId = int.TryParse(User.FindFirst(ServerApiKeyDefaults.ServerIdClaim)?.Value, out var id) ? id : (int?)null;
        var kick = await _moderation.LogKickAsync(request.UserId, request.Nickname, request.Reason, request.IssuedBy, serverId);
        await LogAsync("kick.log", $"{request.IssuedBy} kicked {request.Nickname} ({request.UserId}): {request.Reason}");

        return Ok(new KickDto { Id = kick.Id, Reason = kick.Reason, IssuedBy = kick.IssuedBy, IssuedAt = kick.IssuedAt });
    }

    [HttpGet("history/{userId}")]
    public async Task<ActionResult<PlayerHistoryResponse>> GetHistory(string userId)
    {
        var history = await _moderation.GetHistoryAsync(userId);
        if (history is null) return NotFound(new { message = "No records found for this player." });

        var now = DateTime.UtcNow;
        return Ok(new PlayerHistoryResponse
        {
            UserId = history.UserId,
            LatestNickname = history.LatestNickname,
            FirstSeenAt = history.FirstSeenAt,
            LastSeenAt = history.LastSeenAt,
            Bans = history.Bans.Select(b => new BanDto
            {
                Id = b.Id, Reason = b.Reason, IssuedBy = b.IssuedBy, IssuedAt = b.IssuedAt,
                ExpiresAt = b.ExpiresAt, RevokedAt = b.RevokedAt, RevokedBy = b.RevokedBy, IsActive = b.IsActive(now),
            }).ToList(),
            Mutes = history.Mutes.Select(m => new MuteDto
            {
                Id = m.Id, Reason = m.Reason, IssuedBy = m.IssuedBy, IssuedAt = m.IssuedAt,
                ExpiresAt = m.ExpiresAt, RevokedAt = m.RevokedAt, RevokedBy = m.RevokedBy, IsActive = m.IsActive(now),
            }).ToList(),
            Warnings = history.Warnings.Select(w => new WarningDto
            {
                Id = w.Id, Reason = w.Reason, IssuedBy = w.IssuedBy, IssuedAt = w.IssuedAt,
            }).ToList(),
            Kicks = history.Kicks.Select(k => new KickDto
            {
                Id = k.Id, Reason = k.Reason, IssuedBy = k.IssuedBy, IssuedAt = k.IssuedAt,
            }).ToList(),
        });
    }

    private async Task LogAsync(string action, string details)
    {
        _db.AuditLogEntries.Add(new AuditLogEntry { Actor = ServerName, Action = action, Details = details });
        await _db.SaveChangesAsync();
    }
}
