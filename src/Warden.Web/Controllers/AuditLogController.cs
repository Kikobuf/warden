using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Warden.Web.Data;
using Warden.Web.Models;

namespace Warden.Web.Controllers;

[Authorize(Policy = Permissions.AuditLogView)]
[Route("audit-log")]
public class AuditLogController : Controller
{
    private readonly WardenDbContext _db;

    public AuditLogController(WardenDbContext db)
    {
        _db = db;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1)
    {
        const int pageSize = 50;
        var total = await _db.AuditLogEntries.CountAsync();
        var entries = await _db.AuditLogEntries
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
        return View(entries);
    }
}
