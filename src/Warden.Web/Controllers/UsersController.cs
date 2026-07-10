using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Warden.Web.Data;
using Warden.Web.Models;
using Warden.Web.Services;

namespace Warden.Web.Controllers;

[Authorize(Policy = Permissions.UsersManage)]
[Route("users")]
public class UsersController : Controller
{
    private readonly WardenDbContext _db;
    private readonly CurrentUserService _currentUser;

    public UsersController(WardenDbContext db, CurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var users = await _db.WebUsers
            .Include(u => u.RoleAssignments).ThenInclude(ra => ra.WebRole)
            .OrderBy(u => u.Username)
            .ToListAsync();

        ViewBag.Roles = await _db.WebRoles.OrderBy(r => r.SortOrder).ToListAsync();
        return View(users);
    }

    [HttpPost("{id:int}/roles")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRoles(int id, int[] roleIds)
    {
        var user = await _db.WebUsers.Include(u => u.RoleAssignments).FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();

        var defaultRole = await _db.WebRoles.FirstAsync(r => r.IsDefaultRole);
        var keepIds = roleIds.Append(defaultRole.Id).Distinct().ToHashSet();

        _db.UserRoleAssignments.RemoveRange(user.RoleAssignments);
        foreach (var roleId in keepIds)
        {
            _db.UserRoleAssignments.Add(new UserRoleAssignment { WebUserId = id, WebRoleId = roleId });
        }

        var actor = await _currentUser.GetCurrentUserAsync();
        _db.AuditLogEntries.Add(new AuditLogEntry
        {
            Actor = actor?.Username ?? "unknown",
            Action = "user.roles_update",
            Details = $"Updated roles for user '{user.Username}'",
        });

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/toggle-disabled")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleDisabled(int id)
    {
        var user = await _db.WebUsers.FindAsync(id);
        if (user is null) return NotFound();

        user.IsDisabled = !user.IsDisabled;

        var actor = await _currentUser.GetCurrentUserAsync();
        _db.AuditLogEntries.Add(new AuditLogEntry
        {
            Actor = actor?.Username ?? "unknown",
            Action = user.IsDisabled ? "user.disable" : "user.enable",
            Details = $"{(user.IsDisabled ? "Disabled" : "Enabled")} user '{user.Username}'",
        });

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
