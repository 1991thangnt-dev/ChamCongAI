using ChamCongAI.Data;
using ChamCongAI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChamCongAI.Controllers;

[Authorize(Roles = VaiTro.NhanSu)]
public class NhatKyController : Controller
{
    private readonly AppDbContext _db;
    public NhatKyController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var ds = await _db.ChatLogs.Include(l => l.NguoiDung)
            .OrderByDescending(l => l.ThoiGian).Take(100).ToListAsync();
        return View(ds);
    }
}
