using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Warden.Web.Data;
using Warden.Web.Models;
using Warden.Web.Services;

namespace Warden.Web.Controllers;

[Authorize(Policy = Permissions.RolesManage)]
[Route("roles")]
public class RolesController : Controller
{
    private readonly WardenDbContext _db;
    private readonly CurrentUserService _currentUser;

    public RolesController(WardenDbContext db, CurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var roles = await _db.WebRoles.Include(r => r.Permissions).OrderBy(r => r.SortOrder).ToListAsync();
        ViewBag.PermissionGroups = Models.Permissions.Groups;
        return View(roles);
    }

    [HttpGet("create")]
    public IActionResult Create() => View();

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError(nameof(name), "Name is required.");
            return View();
        }

        if (await _db.WebRoles.AnyAsync(r => r.Name == name))
        {
            ModelState.AddModelError(nameof(name), "A role with this name already exists.");
            return View();
        }

        var maxSort = await _db.WebRoles.MaxAsync(r => (int?)r.SortOrder) ?? 0;
        var role = new WebRole { Name = name.Trim(), Description = description, SortOrder = maxSort + 1 };
        _db.WebRoles.Add(role);

        await LogAsync("role.create", $"Created role '{role.Name}'");
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/permissions")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePermissions(int id, string[] permissionKeys)
    {
        var role = await _db.WebRoles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == id);
        if (role is null) return NotFound();

        if (role.IsOwnerRole)
        {
            // Owner always has everything; editing its permission list is a no-op by design.
            return RedirectToAction(nameof(Index));
        }

        _db.RolePermissions.RemoveRange(role.Permissions);
        var validKeys = Models.Permissions.All.ToHashSet();
        foreach (var key in permissionKeys.Where(validKeys.Contains).Distinct())
        {
            _db.RolePermissions.Add(new RolePermission { WebRoleId = id, PermissionKey = key });
        }

        await LogAsync("role.permissions_update", $"Updated permissions for role '{role.Name}'");
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var role = await _db.WebRoles.FindAsync(id);
        if (role is null) return NotFound();

        if (role.IsOwnerRole || role.IsDefaultRole)
        {
            TempData["Error"] = "The Owner and default roles cannot be deleted.";
            return RedirectToAction(nameof(Index));
        }

        _db.WebRoles.Remove(role);
        await LogAsync("role.delete", $"Deleted role '{role.Name}'");
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private async Task LogAsync(string action, string details)
    {
        var user = await _currentUser.GetCurrentUserAsync();
        _db.AuditLogEntries.Add(new AuditLogEntry { Actor = user?.Username ?? "unknown", Action = action, Details = details });
    }
}
