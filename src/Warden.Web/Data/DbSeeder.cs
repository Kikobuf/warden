using Microsoft.EntityFrameworkCore;
using Warden.Web.Models;

namespace Warden.Web.Data;

/// <summary>
/// Ensures the database exists and that at least one usable role setup is present:
/// an "Owner" role (all permissions, cannot be removed) and a default "Staff" role
/// (everyone gets it automatically, starts with no permissions so it's safe by default).
/// Also grants the Owner role to any Discord user ID listed in Warden:BootstrapAdminDiscordIds
/// the first time they're seen, so the very first login has somewhere to start from.
/// </summary>
public static class DbSeeder
{
    public const string OwnerRoleName = "Owner";
    public const string DefaultRoleName = "Staff";

    public static async Task SeedAsync(WardenDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        if (!await db.WebRoles.AnyAsync(r => r.IsOwnerRole))
        {
            db.WebRoles.Add(new WebRole
            {
                Name = OwnerRoleName,
                Description = "Full, unrestricted access to every part of the site. Cannot be deleted.",
                IsOwnerRole = true,
                SortOrder = 0,
            });
        }

        if (!await db.WebRoles.AnyAsync(r => r.IsDefaultRole))
        {
            db.WebRoles.Add(new WebRole
            {
                Name = DefaultRoleName,
                Description = "Automatically granted to every web user. Starts with no permissions.",
                IsDefaultRole = true,
                SortOrder = 1,
            });
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Called on every login: ensures the user has the default role, and grants Owner if bootstrap-listed.</summary>
    public static async Task EnsureUserRolesAsync(WardenDbContext db, WebUser user, IReadOnlyList<string> bootstrapAdminIds)
    {
        var defaultRole = await db.WebRoles.FirstAsync(r => r.IsDefaultRole);

        var alreadyHasDefault = await db.UserRoleAssignments
            .AnyAsync(ura => ura.WebUserId == user.Id && ura.WebRoleId == defaultRole.Id);

        if (!alreadyHasDefault)
        {
            db.UserRoleAssignments.Add(new UserRoleAssignment { WebUserId = user.Id, WebRoleId = defaultRole.Id });
        }

        if (bootstrapAdminIds.Contains(user.DiscordUserId))
        {
            var ownerRole = await db.WebRoles.FirstAsync(r => r.IsOwnerRole);
            var alreadyOwner = await db.UserRoleAssignments
                .AnyAsync(ura => ura.WebUserId == user.Id && ura.WebRoleId == ownerRole.Id);

            if (!alreadyOwner)
            {
                db.UserRoleAssignments.Add(new UserRoleAssignment { WebUserId = user.Id, WebRoleId = ownerRole.Id });
            }
        }

        await db.SaveChangesAsync();
    }
}
