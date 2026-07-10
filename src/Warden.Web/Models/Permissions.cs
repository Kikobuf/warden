namespace Warden.Web.Models;

/// <summary>
/// Canonical list of every permission key in the website-only permission system.
/// These control access to pages/actions on this site. They have nothing to do with
/// in-game permissions — the game plugin is responsible for deciding who can run
/// in-game commands; it calls the Moderation API using the server's API key, not
/// a per-staff-member web permission.
/// </summary>
public static class Permissions
{
    public const string DashboardView = "dashboard.view";

    public const string ServersView = "servers.view";
    public const string ServersManage = "servers.manage";

    public const string PluginsView = "plugins.view";
    public const string PluginsManage = "plugins.manage";

    public const string ModerationView = "moderation.view";
    public const string ModerationBan = "moderation.ban";
    public const string ModerationMute = "moderation.mute";
    public const string ModerationWarn = "moderation.warn";
    public const string ModerationKick = "moderation.kick";
    public const string ModerationRevoke = "moderation.revoke";

    public const string RolesManage = "roles.manage";
    public const string UsersManage = "users.manage";

    public const string AuditLogView = "auditlog.view";

    /// <summary>All permission keys, grouped for display in the Roles admin UI.</summary>
    public static readonly IReadOnlyList<PermissionGroup> Groups = new List<PermissionGroup>
    {
        new("Dashboard", new[]
        {
            new PermissionDefinition(DashboardView, "View dashboard"),
        }),
        new("Servers", new[]
        {
            new PermissionDefinition(ServersView, "View servers and their status"),
            new PermissionDefinition(ServersManage, "Create, edit, delete servers and regenerate API keys"),
        }),
        new("Plugins", new[]
        {
            new PermissionDefinition(PluginsView, "View installed plugins"),
            new PermissionDefinition(PluginsManage, "Upload, delete, and assign plugins to servers"),
        }),
        new("Moderation", new[]
        {
            new PermissionDefinition(ModerationView, "View player moderation history"),
            new PermissionDefinition(ModerationBan, "Issue bans"),
            new PermissionDefinition(ModerationMute, "Issue mutes"),
            new PermissionDefinition(ModerationWarn, "Issue warnings"),
            new PermissionDefinition(ModerationKick, "Log kicks"),
            new PermissionDefinition(ModerationRevoke, "Revoke (unban / unmute) existing actions"),
        }),
        new("Administration", new[]
        {
            new PermissionDefinition(RolesManage, "Create, edit, delete roles and their permissions"),
            new PermissionDefinition(UsersManage, "Assign roles to web users, disable accounts"),
            new PermissionDefinition(AuditLogView, "View the audit log"),
        }),
    };

    public static readonly IReadOnlyList<string> All = Groups
        .SelectMany(g => g.Permissions)
        .Select(p => p.Key)
        .ToList();
}

public record PermissionGroup(string Name, IReadOnlyList<PermissionDefinition> Permissions);

public record PermissionDefinition(string Key, string Description);
