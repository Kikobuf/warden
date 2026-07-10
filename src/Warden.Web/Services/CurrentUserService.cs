using Microsoft.EntityFrameworkCore;
using Warden.Web.Data;
using Warden.Web.Models;

namespace Warden.Web.Services;

/// <summary>Convenience wrapper for looking up the signed-in WebUser and their effective permissions in Razor views/controllers.</summary>
public class CurrentUserService
{
    private readonly WardenDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(WardenDbContext db, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<WebUser?> GetCurrentUserAsync()
    {
        var discordId = _httpContextAccessor.HttpContext?.User.FindFirst(WardenClaimTypes.DiscordUserId)?.Value;
        if (string.IsNullOrEmpty(discordId))
        {
            return null;
        }

        return await _db.WebUsers
            .Include(u => u.RoleAssignments)
                .ThenInclude(ra => ra.WebRole)
                    .ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.DiscordUserId == discordId);
    }

    public async Task<HashSet<string>> GetEffectivePermissionsAsync()
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return new HashSet<string>();
        }

        if (user.RoleAssignments.Any(ra => ra.WebRole.IsOwnerRole))
        {
            return new HashSet<string>(Permissions.All);
        }

        return user.RoleAssignments
            .SelectMany(ra => ra.WebRole.Permissions)
            .Select(p => p.PermissionKey)
            .ToHashSet();
    }
}
