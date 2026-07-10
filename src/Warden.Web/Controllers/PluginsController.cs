using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Warden.Web.Data;
using Warden.Web.Models;
using Warden.Web.Services;

namespace Warden.Web.Controllers;

[Authorize]
[Route("plugins")]
public class PluginsController : Controller
{
    private readonly WardenDbContext _db;
    private readonly PluginStorageService _storage;
    private readonly CurrentUserService _currentUser;

    public PluginsController(WardenDbContext db, PluginStorageService storage, CurrentUserService currentUser)
    {
        _db = db;
        _storage = storage;
        _currentUser = currentUser;
    }

    [HttpGet("")]
    [Authorize(Policy = Permissions.PluginsView)]
    public async Task<IActionResult> Index()
    {
        var plugins = await _db.Plugins.Include(p => p.Assignments).ThenInclude(a => a.GameServer)
            .OrderBy(p => p.Name).ToListAsync();
        ViewBag.Servers = await _db.GameServers.OrderBy(s => s.Name).ToListAsync();
        return View(plugins);
    }

    [HttpGet("upload")]
    [Authorize(Policy = Permissions.PluginsManage)]
    public IActionResult Upload() => View();

    [HttpPost("upload")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.PluginsManage)]
    [RequestSizeLimit(100 * 1024 * 1024)]
    public async Task<IActionResult> Upload(string name, string version, string? description, bool isGlobal, IFormFile file)
    {
        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError(nameof(file), "Please choose a .dll file to upload.");
            return View();
        }

        if (!file.FileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(file), "Only .dll files may be uploaded.");
            return View();
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError(nameof(name), "Name is required.");
            return View();
        }

        await using var stream = file.OpenReadStream();
        var (storedFileName, hash, size) = await _storage.SaveAsync(stream);

        var user = await _currentUser.GetCurrentUserAsync();
        var plugin = new Plugin
        {
            Name = name.Trim(),
            Version = string.IsNullOrWhiteSpace(version) ? "1.0.0" : version.Trim(),
            Description = description,
            StoredFileName = storedFileName,
            OriginalFileName = file.FileName,
            Sha256Hash = hash,
            FileSizeBytes = size,
            UploadedAt = DateTime.UtcNow,
            UploadedBy = user?.Username ?? "unknown",
            IsGlobal = isGlobal,
        };

        _db.Plugins.Add(plugin);
        _db.AuditLogEntries.Add(new AuditLogEntry
        {
            Actor = user?.Username ?? "unknown",
            Action = "plugin.upload",
            Details = $"Uploaded plugin '{plugin.Name}' v{plugin.Version} ({(isGlobal ? "global" : "not yet assigned")})",
        });
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/assign")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.PluginsManage)]
    public async Task<IActionResult> Assign(int id, int[] serverIds)
    {
        var plugin = await _db.Plugins.Include(p => p.Assignments).FirstOrDefaultAsync(p => p.Id == id);
        if (plugin is null) return NotFound();

        _db.PluginAssignments.RemoveRange(plugin.Assignments);
        foreach (var serverId in serverIds.Distinct())
        {
            _db.PluginAssignments.Add(new PluginAssignment { PluginId = id, GameServerId = serverId });
        }

        var user = await _currentUser.GetCurrentUserAsync();
        _db.AuditLogEntries.Add(new AuditLogEntry
        {
            Actor = user?.Username ?? "unknown",
            Action = "plugin.assign",
            Details = $"Updated server assignments for plugin '{plugin.Name}' ({serverIds.Length} server(s))",
        });

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/toggle-global")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.PluginsManage)]
    public async Task<IActionResult> ToggleGlobal(int id)
    {
        var plugin = await _db.Plugins.FindAsync(id);
        if (plugin is null) return NotFound();

        plugin.IsGlobal = !plugin.IsGlobal;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.PluginsManage)]
    public async Task<IActionResult> Delete(int id)
    {
        var plugin = await _db.Plugins.FindAsync(id);
        if (plugin is null) return NotFound();

        _storage.Delete(plugin.StoredFileName);
        _db.Plugins.Remove(plugin);

        var user = await _currentUser.GetCurrentUserAsync();
        _db.AuditLogEntries.Add(new AuditLogEntry
        {
            Actor = user?.Username ?? "unknown",
            Action = "plugin.delete",
            Details = $"Deleted plugin '{plugin.Name}'",
        });

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
