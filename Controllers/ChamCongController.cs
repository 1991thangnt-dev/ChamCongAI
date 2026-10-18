using System.Globalization;
using System.Security.Claims;
using ChamCongAI.Data;
using ChamCongAI.Models;
using ChamCongAI.Services;
using ChamCongAI.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChamCongAI.Controllers;

[Authorize]
public class ChamCongController : Controller
{
    private readonly AppDbContext _db;
    private readonly ChatTools _tools;

    public ChamCongController(AppDbContext db, ChatTools tools)
    {
        _db = db; _tools = tools;
    }

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static DateTime DauThang(string thang)
    {
        if (DateTime.TryParseExact(thang, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return new DateTime(d.Year, d.Month, 1);
        var hn = DateTime.Today;
        return new DateTime(hn.Year, hn.Month, 1);
    }

    public async Task<IActionResult> Index(string thang)
    {
        var dau = DauThang(thang);
        var cuoi = dau.AddMonths(1).AddDays(-1);
        var me = await _db.NguoiDungs.FindAsync(UserId);

        var vm = new ChamCongThangVm
        {
            Thang = dau,
            Ds = await _db.ChamCongs.Where(x => x.NguoiDungId == UserId && x.Ngay >= dau && x.Ngay <= cuoi)
                .OrderByDescending(x => x.Ngay).ToListAsync(),
            HomNay = await _db.ChamCongs.FirstOrDefaultAsync(x => x.NguoiDungId == UserId && x.Ngay == DateTime.Today),
            ThongKe = await _tools.ThongKeChamCongAsync(me, dau, cuoi)
        };
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> CheckIn()
    {
        var hn = DateTime.Today;
        if (!LichLam.LaNgayLam(hn))
        {
            TempData["Loi"] = "Hôm nay là ngày nghỉ cuối tuần.";
            return RedirectToAction(nameof(Index));
        }
        var cc = await _db.ChamCongs.FirstOrDefaultAsync(x => x.NguoiDungId == UserId && x.Ngay == hn);
        if (cc?.GioVao != null)
        {
            TempData["Loi"] = "Bạn đã chấm công vào hôm nay rồi.";
            return RedirectToAction(nameof(Index));
        }

        var vao = DateTime.Now.TimeOfDay;
        var (tre, _) = LichLam.TinhTreSom(vao, null);
        _db.ChamCongs.Add(new ChamCong { NguoiDungId = UserId, Ngay = hn, GioVao = vao, DiTrePhut = tre });
        await _db.SaveChangesAsync();

        TempData["Ok"] = tre > 0 ? $"Đã chấm công vào. Bạn đi trễ {tre} phút." : "Đã chấm công vào đúng giờ.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> CheckOut()
    {
        var hn = DateTime.Today;
        var cc = await _db.ChamCongs.FirstOrDefaultAsync(x => x.NguoiDungId == UserId && x.Ngay == hn);
        if (cc?.GioVao == null)
        {
            TempData["Loi"] = "Bạn chưa chấm công vào hôm nay.";
            return RedirectToAction(nameof(Index));
        }
        if (cc.GioRa != null)
        {
            TempData["Loi"] = "Bạn đã chấm công ra rồi.";
            return RedirectToAction(nameof(Index));
        }

        cc.GioRa = DateTime.Now.TimeOfDay;
        var (_, som) = LichLam.TinhTreSom(cc.GioVao, cc.GioRa);
        cc.VeSomPhut = som;
        await _db.SaveChangesAsync();

        TempData["Ok"] = som > 0 ? $"Đã chấm công ra. Bạn về sớm {som} phút." : "Đã chấm công ra.";
        return RedirectToAction(nameof(Index));
    }

    // Tổng hợp theo tháng: nhân sự xem tất cả, quản lý xem nhân viên của mình
    [Authorize(Roles = VaiTro.QuanLy + "," + VaiTro.NhanSu)]
    public async Task<IActionResult> TongHop(string thang)
    {
        var dau = DauThang(thang);
        var cuoi = dau.AddMonths(1).AddDays(-1);

        var q = _db.NguoiDungs.AsQueryable();
        if (User.IsInRole(VaiTro.QuanLy)) q = q.Where(x => x.QuanLyId == UserId);
        var nguoi = await q.OrderBy(x => x.HoTen).ToListAsync();

        var ds = new List<TongHopDongVm>();
        foreach (var n in nguoi)
            ds.Add(new TongHopDongVm { NhanVien = n, ThongKe = await _tools.ThongKeChamCongAsync(n, dau, cuoi) });

        ViewBag.Thang = dau;
        return View(ds);
    }
}
