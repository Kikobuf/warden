using System.ComponentModel.DataAnnotations;

namespace Warden.Web.Models;

/// <summary>
/// A record of a meaningful action taken on the site (moderation actions, plugin changes,
/// server changes, role/permission changes). Helpful for accountability across a staff team;
/// does not interact with the game servers or plugins in any way.
/// </summary>
public class AuditLogEntry
{
    public int Id { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Who performed the action — a web username, or a server name for API-driven actions.</summary>
    [MaxLength(128)]
    public string Actor { get; set; } = string.Empty;

    /// <summary>Short machine-friendly action name, e.g. "ban.issue", "plugin.upload".</summary>
    [MaxLength(64)]
    public string Action { get; set; } = string.Empty;

    /// <summary>Human-readable details of what happened.</summary>
    [MaxLength(1024)]
    public string Details { get; set; } = string.Empty;
}
