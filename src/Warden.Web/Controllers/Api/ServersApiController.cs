using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Warden.Web.Data;
using Warden.Web.Models;
using Warden.Web.Models.Dtos;
using Warden.Web.Services;

namespace Warden.Web.Controllers.Api;

/// <summary>
/// Server-to-server API for heartbeats and plugin distribution. Authenticated via the
/// "X-Server-Api-Key" header, same as the moderation API. See docs/API.md.
/// </summary>
[ApiController]
[Route("api/v1/servers")]
[Authorize(AuthenticationSchemes = ServerApiKeyDefaults.AuthenticationScheme)]
public class ServersApiController : ControllerBase
{
    private readonly WardenDbContext _db;
    private readonly ModerationService _moderation;
    private readonly PluginStorageService _pluginStorage;

    public ServersApiController(WardenDbContext db, ModerationService moderation, PluginStorageService pluginStorage)
    {
        _db = db;
        _moderation = moderation;
        _pluginStorage = pluginStorage;
    }

    private int ServerId => int.Parse(User.FindFirst(ServerApiKeyDefaults.ServerIdClaim)!.Value);

    /// <summary>
    /// Called by each game server roughly every 30 seconds with its current player list and TPS.
    /// Replaces the server's tracked player sessions wholesale (anyone not in this list is considered gone).
    /// </summary>
    [HttpPost("heartbeat")]
    public async Task<ActionResult<HeartbeatResponse>> Heartbeat(HeartbeatRequest request)
    {
        var server = await _db.GameServers
            .Include(s => s.PlayerSessions)
            .FirstOrDefaultAsync(s => s.Id == ServerId);

        if (server is null) return Unauthorized();

        var now = DateTime.UtcNow;
        server.LastHeartbeatAt = now;
        server.LastTps = request.Tps;
        server.LastPlayerCount = request.Players.Count;

        _db.ServerPlayerSessions.RemoveRange(server.PlayerSessions);

        foreach (var p in request.Players)
        {
            _db.ServerPlayerSessions.Add(new ServerPlayerSession
            {
                GameServerId = server.Id,
                PlayerUserId = p.UserId,
                PlayerNickname = p.Nickname,
                LastSeenAt = now,
            });

            await _moderation.TouchPlayerAsync(p.UserId, p.Nickname);
        }

        await _db.SaveChangesAsync();

        return Ok(new HeartbeatResponse { ServerName = server.Name, ReceivedAt = now });
    }

    /// <summary>
    /// Called by each server on startup to discover which plugins it should download.
    /// Includes every plugin explicitly assigned to this server plus every plugin marked global.
    /// </summary>
    [HttpGet("plugins")]
    public async Task<ActionResult<List<PluginManifestEntry>>> GetAssignedPlugins()
    {
        var plugins = await _db.Plugins
            .Where(p => p.IsGlobal || p.Assignments.Any(a => a.GameServerId == ServerId))
            .ToListAsync();

        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var manifest = plugins.Select(p => new PluginManifestEntry
        {
            Id = p.Id,
            Name = p.Name,
            Version = p.Version,
            FileName = p.OriginalFileName,
            Sha256Hash = p.Sha256Hash,
            FileSizeBytes = p.FileSizeBytes,
            DownloadUrl = $"{baseUrl}/api/v1/servers/plugins/{p.Id}/download",
        }).ToList();

        return Ok(manifest);
    }

    /// <summary>Streams the plugin DLL bytes. Only servers the plugin is assigned to (or global plugins) may download it.</summary>
    [HttpGet("plugins/{pluginId}/download")]
    public async Task<IActionResult> DownloadPlugin(int pluginId)
    {
        var plugin = await _db.Plugins
            .Include(p => p.Assignments)
            .FirstOrDefaultAsync(p => p.Id == pluginId);

        if (plugin is null) return NotFound();

        var isAllowed = plugin.IsGlobal || plugin.Assignments.Any(a => a.GameServerId == ServerId);
        if (!isAllowed) return Forbid();

        var stream = _pluginStorage.OpenRead(plugin.StoredFileName);
        return File(stream, "application/octet-stream", plugin.OriginalFileName);
    }
}
