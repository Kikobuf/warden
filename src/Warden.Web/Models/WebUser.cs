using System.ComponentModel.DataAnnotations;

namespace Warden.Web.Models;

/// <summary>
/// A human who can log into the Warden website via Discord. This is completely separate
/// from in-game players (see <see cref="PlayerProfile"/>) — a web user is a staff member
/// who administers servers; a player profile is anyone who has ever joined a game server.
/// </summary>
public class WebUser
{
    public int Id { get; set; }

    /// <summary>The Discord snowflake ID for this user. This is the permanent, unique identifier.</summary>
    [Required]
    [MaxLength(32)]
    public string DiscordUserId { get; set; } = string.Empty;

    /// <summary>Discord username at last login (display purposes only, not an identifier).</summary>
    [MaxLength(64)]
    public string Username { get; set; } = string.Empty;

    /// <summary>Discord avatar hash, used to build the avatar CDN URL. May be null (default avatar).</summary>
    [MaxLength(64)]
    public string? AvatarHash { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    /// <summary>If true, this account is blocked from logging in regardless of role/permissions.</summary>
    public bool IsDisabled { get; set; }

    public List<UserRoleAssignment> RoleAssignments { get; set; } = new();
}

/// <summary>A named collection of permissions that can be assigned to web users.</summary>
public class WebRole
{
    public int Id { get; set; }

    [Required]
    [MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? Description { get; set; }

    /// <summary>
    /// The built-in "Owner" role — always has every permission and cannot be deleted or
    /// edited away to zero permissions. Prevents a site from accidentally locking everyone out.
    /// </summary>
    public bool IsOwnerRole { get; set; }

    /// <summary>If true, every web user is implicitly a member of this role (e.g. a base "Staff" role).</summary>
    public bool IsDefaultRole { get; set; }

    /// <summary>Display order in the UI, lower = higher up the list.</summary>
    public int SortOrder { get; set; }

    public List<RolePermission> Permissions { get; set; } = new();

    public List<UserRoleAssignment> UserAssignments { get; set; } = new();
}

/// <summary>Join table: which permission keys a role grants. See <see cref="Permissions"/> for valid keys.</summary>
public class RolePermission
{
    public int Id { get; set; }

    public int WebRoleId { get; set; }
    public WebRole WebRole { get; set; } = null!;

    [Required]
    [MaxLength(64)]
    public string PermissionKey { get; set; } = string.Empty;
}

/// <summary>Join table: which roles a web user holds.</summary>
public class UserRoleAssignment
{
    public int Id { get; set; }

    public int WebUserId { get; set; }
    public WebUser WebUser { get; set; } = null!;

    public int WebRoleId { get; set; }
    public WebRole WebRole { get; set; } = null!;
}
