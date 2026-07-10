using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Warden.Web.Data;

namespace Warden.Web.Services;

/// <summary>An authorization requirement satisfied only if the current web user holds the given permission key.</summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    public string PermissionKey { get; }
    public PermissionRequirement(string permissionKey) => PermissionKey = permissionKey;
}

/// <summary>
/// Checks the current permission requirement against the signed-in web user's roles, read
/// fresh from the database on every check. This is deliberately not cached in claims so that
/// permission changes (role edits, reassignment) take effect immediately without re-login.
/// </summary>
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly WardenDbContext _db;

    public PermissionAuthorizationHandler(WardenDbContext db)
    {
        _db = db;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var discordId = context.User.FindFirst(WardenClaimTypes.DiscordUserId)?.Value;
        if (string.IsNullOrEmpty(discordId))
        {
            return;
        }

        var user = await _db.WebUsers
            .Include(u => u.RoleAssignments)
                .ThenInclude(ra => ra.WebRole)
                    .ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.DiscordUserId == discordId);

        if (user is null || user.IsDisabled)
        {
            return;
        }

        var hasPermission = user.RoleAssignments.Any(ra =>
            ra.WebRole.IsOwnerRole ||
            ra.WebRole.Permissions.Any(p => p.PermissionKey == requirement.PermissionKey));

        if (hasPermission)
        {
            context.Succeed(requirement);
        }
    }
}

public static class WardenClaimTypes
{
    public const string DiscordUserId = "warden:discord_user_id";
}

public static class PermissionPolicies
{
    /// <summary>Registers one authorization policy per permission key, named identically to the key.</summary>
    public static void AddAllPermissionPolicies(this AuthorizationOptions options)
    {
        foreach (var key in Models.Permissions.All)
        {
            options.AddPolicy(key, policy => policy.Requirements.Add(new PermissionRequirement(key)));
        }
    }
}
