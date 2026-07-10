using Microsoft.EntityFrameworkCore;
using Warden.Web.Data;
using Warden.Web.Models;

namespace Warden.Web.Services;

/// <summary>
/// All moderation business logic in one place, so the API controllers (used by the game
/// plugin) and the MVC controllers (used by staff on the website) behave identically.
/// </summary>
public class ModerationService
{
    private readonly WardenDbContext _db;

    public ModerationService(WardenDbContext db)
    {
        _db = db;
    }

    public async Task<PlayerProfile> TouchPlayerAsync(string userId, string nickname)
    {
        var player = await _db.PlayerProfiles.FindAsync(userId);
        var now = DateTime.UtcNow;

        if (player is null)
        {
            player = new PlayerProfile
            {
                UserId = userId,
                LatestNickname = nickname,
                FirstSeenAt = now,
                LastSeenAt = now,
            };
            _db.PlayerProfiles.Add(player);
        }
        else
        {
            player.LatestNickname = nickname;
            player.LastSeenAt = now;
        }

        return player;
    }

    public record ModerationStatus(
        bool IsBanned, string? BanReason, DateTime? BanExpiresAt, string? BannedBy,
        bool IsMuted, string? MuteReason, DateTime? MuteExpiresAt, string? MutedBy);

    public async Task<ModerationStatus> GetStatusAsync(string userId)
    {
        var now = DateTime.UtcNow;

        var activeBan = await _db.Bans
            .Where(b => b.PlayerUserId == userId && b.RevokedAt == null)
            .Where(b => b.ExpiresAt == null || b.ExpiresAt > now)
            .OrderByDescending(b => b.IssuedAt)
            .FirstOrDefaultAsync();

        var activeMute = await _db.Mutes
            .Where(m => m.PlayerUserId == userId && m.RevokedAt == null)
            .Where(m => m.ExpiresAt == null || m.ExpiresAt > now)
            .OrderByDescending(m => m.IssuedAt)
            .FirstOrDefaultAsync();

        return new ModerationStatus(
            activeBan is not null, activeBan?.Reason, activeBan?.ExpiresAt, activeBan?.IssuedBy,
            activeMute is not null, activeMute?.Reason, activeMute?.ExpiresAt, activeMute?.IssuedBy);
    }

    public async Task<Ban> IssueBanAsync(string userId, string nickname, string reason, string issuedBy, TimeSpan? duration)
    {
        await TouchPlayerAsync(userId, nickname);

        var ban = new Ban
        {
            PlayerUserId = userId,
            PlayerNickname = nickname,
            Reason = reason,
            IssuedBy = issuedBy,
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = duration is null ? null : DateTime.UtcNow.Add(duration.Value),
        };
        _db.Bans.Add(ban);
        await _db.SaveChangesAsync();
        return ban;
    }

    public async Task<bool> RevokeBanAsync(string userId, string revokedBy, string? reason)
    {
        var now = DateTime.UtcNow;
        var activeBan = await _db.Bans
            .Where(b => b.PlayerUserId == userId && b.RevokedAt == null)
            .Where(b => b.ExpiresAt == null || b.ExpiresAt > now)
            .OrderByDescending(b => b.IssuedAt)
            .FirstOrDefaultAsync();

        if (activeBan is null) return false;

        activeBan.RevokedAt = now;
        activeBan.RevokedBy = revokedBy;
        activeBan.RevokeReason = reason;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<Mute> IssueMuteAsync(string userId, string nickname, string reason, string issuedBy, TimeSpan? duration)
    {
        await TouchPlayerAsync(userId, nickname);

        var mute = new Mute
        {
            PlayerUserId = userId,
            PlayerNickname = nickname,
            Reason = reason,
            IssuedBy = issuedBy,
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = duration is null ? null : DateTime.UtcNow.Add(duration.Value),
        };
        _db.Mutes.Add(mute);
        await _db.SaveChangesAsync();
        return mute;
    }

    public async Task<bool> RevokeMuteAsync(string userId, string revokedBy, string? reason)
    {
        var now = DateTime.UtcNow;
        var activeMute = await _db.Mutes
            .Where(m => m.PlayerUserId == userId && m.RevokedAt == null)
            .Where(m => m.ExpiresAt == null || m.ExpiresAt > now)
            .OrderByDescending(m => m.IssuedAt)
            .FirstOrDefaultAsync();

        if (activeMute is null) return false;

        activeMute.RevokedAt = now;
        activeMute.RevokedBy = revokedBy;
        activeMute.RevokeReason = reason;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<Warning> IssueWarningAsync(string userId, string nickname, string reason, string issuedBy)
    {
        await TouchPlayerAsync(userId, nickname);

        var warning = new Warning
        {
            PlayerUserId = userId,
            PlayerNickname = nickname,
            Reason = reason,
            IssuedBy = issuedBy,
            IssuedAt = DateTime.UtcNow,
        };
        _db.Warnings.Add(warning);
        await _db.SaveChangesAsync();
        return warning;
    }

    public async Task<KickRecord> LogKickAsync(string userId, string nickname, string reason, string issuedBy, int? gameServerId)
    {
        await TouchPlayerAsync(userId, nickname);

        var kick = new KickRecord
        {
            PlayerUserId = userId,
            PlayerNickname = nickname,
            Reason = reason,
            IssuedBy = issuedBy,
            IssuedAt = DateTime.UtcNow,
            GameServerId = gameServerId,
        };
        _db.Kicks.Add(kick);
        await _db.SaveChangesAsync();
        return kick;
    }

    public record PlayerHistory(
        string UserId, string LatestNickname, DateTime FirstSeenAt, DateTime LastSeenAt,
        List<Ban> Bans, List<Mute> Mutes, List<Warning> Warnings, List<KickRecord> Kicks);

    public async Task<PlayerHistory?> GetHistoryAsync(string userId)
    {
        var player = await _db.PlayerProfiles.FindAsync(userId);
        if (player is null) return null;

        var bans = await _db.Bans.Where(b => b.PlayerUserId == userId).OrderByDescending(b => b.IssuedAt).ToListAsync();
        var mutes = await _db.Mutes.Where(m => m.PlayerUserId == userId).OrderByDescending(m => m.IssuedAt).ToListAsync();
        var warnings = await _db.Warnings.Where(w => w.PlayerUserId == userId).OrderByDescending(w => w.IssuedAt).ToListAsync();
        var kicks = await _db.Kicks.Where(k => k.PlayerUserId == userId).OrderByDescending(k => k.IssuedAt).ToListAsync();

        return new PlayerHistory(player.UserId, player.LatestNickname, player.FirstSeenAt, player.LastSeenAt,
            bans, mutes, warnings, kicks);
    }
}
