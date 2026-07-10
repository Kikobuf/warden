using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Warden.Web.Data;
using Warden.Web.Models;
using Warden.Web.Services;

namespace Warden.Web.Controllers;

[Authorize]
[Route("servers")]
public class ServersController : Controller
{
    private readonly WardenDbContext _db;
    private readonly IConfiguration _config;
    private readonly CurrentUserService _currentUser;

    public ServersController(WardenDbContext db, IConfiguration config, CurrentUserService currentUser)
    {
        _db = db;
        _config = config;
        _currentUser = currentUser;
    }

    [HttpGet("")]
    [Authorize(Policy = Permissions.ServersView)]
    public async Task<IActionResult> Index()
    {
        var offlineThreshold = _config.GetValue("Warden:OfflineThresholdSeconds", 90);
        var now = DateTime.UtcNow;
        var servers = await _db.GameServers.OrderBy(s => s.Name).ToListAsync();

        ViewBag.OfflineThreshold = offlineThreshold;
        ViewBag.Now = now;
        return View(servers);
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = Permissions.ServersView)]
    public async Task<IActionResult> Details(int id)
    {
        var offlineThreshold = _config.GetValue("Warden:OfflineThresholdSeconds", 90);
        var server = await _db.GameServers
            .Include(s => s.PlayerSessions)
            .Include(s => s.PluginAssignments).ThenInclude(a => a.Plugin)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (server is null) return NotFound();

        ViewBag.OfflineThreshold = offlineThreshold;
        ViewBag.Now = DateTime.UtcNow;
        return View(server);
    }

    [HttpGet("create")]
    [Authorize(Policy = Permissions.ServersManage)]
    public IActionResult Create() => View();

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.ServersManage)]
    public async Task<IActionResult> Create(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError(nameof(name), "Name is required.");
            return View();
        }

        var plaintextKey = ApiKeyService.GenerateKey();
        var server = new GameServer
        {
            Name = name.Trim(),
            Description = description,
            ApiKeyHash = ApiKeyService.Hash(plaintextKey),
            ApiKeyPrefix = ApiKeyService.DisplayPrefix(plaintextKey),
            CreatedAt = DateTime.UtcNow,
        };

        _db.GameServers.Add(server);
        await LogAsync("server.create", $"Created server '{server.Name}'");
        await _db.SaveChangesAsync();

        TempData["NewApiKey"] = plaintextKey;
        TempData["NewApiKeyServerId"] = server.Id;
        return RedirectToAction(nameof(Details), new { id = server.Id });
    }

    [HttpPost("{id:int}/regenerate-key")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.ServersManage)]
    public async Task<IActionResult> RegenerateKey(int id)
    {
        var server = await _db.GameServers.FindAsync(id);
        if (server is null) return NotFound();

        var plaintextKey = ApiKeyService.GenerateKey();
        server.ApiKeyHash = ApiKeyService.Hash(plaintextKey);
        server.ApiKeyPrefix = ApiKeyService.DisplayPrefix(plaintextKey);

        await LogAsync("server.regenerate_key", $"Regenerated API key for server '{server.Name}'");
        await _db.SaveChangesAsync();

        TempData["NewApiKey"] = plaintextKey;
        TempData["NewApiKeyServerId"] = server.Id;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:int}/delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.ServersManage)]
    public async Task<IActionResult> Delete(int id)
    {
        var server = await _db.GameServers.FindAsync(id);
        if (server is null) return NotFound();

        _db.GameServers.Remove(server);
        await LogAsync("server.delete", $"Deleted server '{server.Name}'");
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    private async Task LogAsync(string action, string details)
    {
        var user = await _currentUser.GetCurrentUserAsync();
        _db.AuditLogEntries.Add(new AuditLogEntry { Actor = user?.Username ?? "unknown", Action = action, Details = details });
    }
}
