using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Warden.Web.Data;
using Warden.Web.Models;
using Warden.Web.Services;

namespace Warden.Web.Controllers;

[Authorize]
[Route("players")]
public class PlayersController : Controller
{
    private readonly WardenDbContext _db;
    private readonly ModerationService _moderation;
    private readonly CurrentUserService _currentUser;

    public PlayersController(WardenDbContext db, ModerationService moderation, CurrentUserService currentUser)
    {
        _db = db;
        _moderation = moderation;
        _currentUser = currentUser;
    }

    [HttpGet("")]
    [Authorize(Policy = Permissions.ModerationView)]
    public async Task<IActionResult> Index(string? q)
    {
        var query = _db.PlayerProfiles.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(p => p.LatestNickname.Contains(q) || p.UserId.Contains(q));
        }

        var players = await query.OrderByDescending(p => p.LastSeenAt).Take(100).ToListAsync();
        ViewBag.Query = q;
        return View(players);
    }

    [HttpGet("{userId}")]
    [Authorize(Policy = Permissions.ModerationView)]
    public async Task<IActionResult> Details(string userId)
    {
        var history = await _moderation.GetHistoryAsync(userId);
        if (history is null) return NotFound();

        return View(history);
    }

    [HttpPost("{userId}/ban")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.ModerationBan)]
    public async Task<IActionResult> Ban(string userId, string nickname, string reason, int? durationHours)
    {
        var user = await _currentUser.GetCurrentUserAsync();
        var duration = durationHours is > 0 ? TimeSpan.FromHours(durationHours.Value) : (TimeSpan?)null;
        await _moderation.IssueBanAsync(userId, nickname, reason, user?.Username ?? "unknown", duration);
        await LogAsync("ban.issue", $"Banned {nickname} ({userId}): {reason}");
        return RedirectToAction(nameof(Details), new { userId });
    }

    [HttpPost("{userId}/unban")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.ModerationRevoke)]
    public async Task<IActionResult> Unban(string userId, string? reason)
    {
        var user = await _currentUser.GetCurrentUserAsync();
        await _moderation.RevokeBanAsync(userId, user?.Username ?? "unknown", reason);
        await LogAsync("ban.revoke", $"Unbanned {userId}");
        return RedirectToAction(nameof(Details), new { userId });
    }

    [HttpPost("{userId}/mute")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.ModerationMute)]
    public async Task<IActionResult> Mute(string userId, string nickname, string reason, int? durationHours)
    {
        var user = await _currentUser.GetCurrentUserAsync();
        var duration = durationHours is > 0 ? TimeSpan.FromHours(durationHours.Value) : (TimeSpan?)null;
        await _moderation.IssueMuteAsync(userId, nickname, reason, user?.Username ?? "unknown", duration);
        await LogAsync("mute.issue", $"Muted {nickname} ({userId}): {reason}");
        return RedirectToAction(nameof(Details), new { userId });
    }

    [HttpPost("{userId}/unmute")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.ModerationRevoke)]
    public async Task<IActionResult> Unmute(string userId, string? reason)
    {
        var user = await _currentUser.GetCurrentUserAsync();
        await _moderation.RevokeMuteAsync(userId, user?.Username ?? "unknown", reason);
        await LogAsync("mute.revoke", $"Unmuted {userId}");
        return RedirectToAction(nameof(Details), new { userId });
    }

    [HttpPost("{userId}/warn")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.ModerationWarn)]
    public async Task<IActionResult> Warn(string userId, string nickname, string reason)
    {
        var user = await _currentUser.GetCurrentUserAsync();
        await _moderation.IssueWarningAsync(userId, nickname, reason, user?.Username ?? "unknown");
        await LogAsync("warning.issue", $"Warned {nickname} ({userId}): {reason}");
        return RedirectToAction(nameof(Details), new { userId });
    }

    [HttpPost("{userId}/kick")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.ModerationKick)]
    public async Task<IActionResult> Kick(string userId, string nickname, string reason)
    {
        var user = await _currentUser.GetCurrentUserAsync();
        await _moderation.LogKickAsync(userId, nickname, reason, user?.Username ?? "unknown", null);
        await LogAsync("kick.log", $"Kicked {nickname} ({userId}): {reason} (recorded from web, plugin performs the actual kick)");
        return RedirectToAction(nameof(Details), new { userId });
    }

    private async Task LogAsync(string action, string details)
    {
        var user = await _currentUser.GetCurrentUserAsync();
        _db.AuditLogEntries.Add(new AuditLogEntry { Actor = user?.Username ?? "unknown", Action = action, Details = details });
        await _db.SaveChangesAsync();
    }
}
