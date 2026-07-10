using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Warden.Web.Data;
using Warden.Web.Models;

namespace Warden.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly WardenDbContext _db;
    private readonly IConfiguration _config;

    public HomeController(WardenDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [Authorize(Policy = Permissions.DashboardView)]
    public async Task<IActionResult> Index()
    {
        var offlineThreshold = _config.GetValue("Warden:OfflineThresholdSeconds", 90);
        var now = DateTime.UtcNow;

        var servers = await _db.GameServers.ToListAsync();
        var vm = new DashboardViewModel
        {
            TotalServers = servers.Count,
            OnlineServers = servers.Count(s => s.IsOnline(now, offlineThreshold)),
            TotalPlayersOnline = servers.Where(s => s.IsOnline(now, offlineThreshold)).Sum(s => s.LastPlayerCount),
            ActiveBans = await _db.Bans.CountAsync(b => b.RevokedAt == null && (b.ExpiresAt == null || b.ExpiresAt > now)),
            ActiveMutes = await _db.Mutes.CountAsync(m => m.RevokedAt == null && (m.ExpiresAt == null || m.ExpiresAt > now)),
            TotalPlugins = await _db.Plugins.CountAsync(),
            RecentAuditEntries = await _db.AuditLogEntries.OrderByDescending(a => a.Timestamp).Take(15).ToListAsync(),
        };

        return View(vm);
    }

    [AllowAnonymous]
    public IActionResult Error() => View();
}

public class DashboardViewModel
{
    public int TotalServers { get; set; }
    public int OnlineServers { get; set; }
    public int TotalPlayersOnline { get; set; }
    public int ActiveBans { get; set; }
    public int ActiveMutes { get; set; }
    public int TotalPlugins { get; set; }
    public List<AuditLogEntry> RecentAuditEntries { get; set; } = new();
}
