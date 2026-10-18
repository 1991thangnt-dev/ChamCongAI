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
public class DonNghiController : Controller
{
    private static readonly string[] DuoiChoPhep = { ".jpg", ".jpeg", ".png", ".pdf" };

    private readonly AppDbContext _db;
    private readonly ChatTools _tools;
    private readonly IWebHostEnvironment _env;

    public DonNghiController(AppDbContext db, ChatTools tools, IWebHostEnvironment env)
    {
        _db = db; _tools = tools; _env = env;
    }

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public async Task<IActionResult> Index()
    {
        var q = _db.DonNghis.Include(d => d.NguoiDung).Include(d => d.NguoiDuyet).AsQueryable();
        if (User.IsInRole(VaiTro.NhanVien))
            q = q.Where(d => d.NguoiDungId == UserId);
        else if (User.IsInRole(VaiTro.QuanLy))
            q = q.Where(d => d.NguoiDungId == UserId || d.NguoiDung.QuanLyId == UserId);

        var ds = await q.OrderByDescending(d => d.NgayTao).ToListAsync();
        ViewBag.UserId = UserId;
        return View(ds);
    }

    public IActionResult Create() => View(new DonNghiVm());

    [HttpPost]
    public async Task<IActionResult> Create(DonNghiVm vm)
    {
        if (vm.DenNgay < vm.TuNgay)
            ModelState.AddModelError(nameof(vm.DenNgay), "Ngày kết thúc phải sau hoặc bằng ngày bắt đầu.");
        if (!ModelState.IsValid) return View(vm);

        var me = await _db.NguoiDungs.FindAsync(UserId);
        var tu = vm.TuNgay.Date;
        var den = vm.DenNgay.Date;

        // Thai sản tính theo ngày lịch, các loại khác chỉ tính ngày làm việc
        int soNgay = vm.Loai == LoaiNghi.ThaiSan ? (den - tu).Days + 1 : LichLam.DemNgayLam(tu, den);
        if (soNgay <= 0)
        {
            ModelState.AddModelError("", "Khoảng ngày bạn chọn không có ngày làm việc nào.");
            return View(vm);
        }

        // Không trùng với đơn khác còn hiệu lực
        bool trung = await _db.DonNghis.AnyAsync(d => d.NguoiDungId == UserId
            && d.TrangThai != TrangThaiDon.TuChoi && d.TuNgay <= den && d.DenNgay >= tu);
        if (trung)
        {
            ModelState.AddModelError("", "Khoảng ngày này trùng với một đơn nghỉ khác của bạn.");
            return View(vm);
        }

        // Phép năm: không vượt số ngày còn lại (đã tính cả đơn đang chờ duyệt)
        if (vm.Loai == LoaiNghi.PhepNam)
        {
            var p = await _tools.PhepConLaiAsync(me, tu.Year);
            if (soNgay > p.SoNgayConLaiSauKhiTruDonDangCho)
            {
                ModelState.AddModelError("", $"Bạn xin nghỉ {soNgay} ngày nhưng chỉ còn {p.SoNgayConLaiSauKhiTruDonDangCho} ngày phép (đã tính các đơn đang chờ duyệt).");
                return View(vm);
            }
        }

        var don = new DonNghi
        {
            NguoiDungId = UserId, Loai = vm.Loai, TuNgay = tu, DenNgay = den,
            SoNgay = soNgay, LyDo = vm.LyDo.Trim()
        };

        if (vm.TepDinhKem != null && vm.TepDinhKem.Length > 0)
        {
            var duoi = Path.GetExtension(vm.TepDinhKem.FileName).ToLowerInvariant();
            if (!DuoiChoPhep.Contains(duoi) || vm.TepDinhKem.Length > 10 * 1024 * 1024)
            {
                ModelState.AddModelError(nameof(vm.TepDinhKem), "Chỉ nhận jpg, png, pdf tối đa 10 MB.");
                return View(vm);
            }
            var thuMuc = Path.Combine(_env.ContentRootPath, "App_Data", "don");
            Directory.CreateDirectory(thuMuc);
            var tenLuu = Guid.NewGuid().ToString("N") + duoi;
            await using (var fs = System.IO.File.Create(Path.Combine(thuMuc, tenLuu)))
                await vm.TepDinhKem.CopyToAsync(fs);
            don.TepDinhKem = tenLuu;
            don.TenTepGoc = Path.GetFileName(vm.TepDinhKem.FileName);
        }

        _db.DonNghis.Add(don);
        await _db.SaveChangesAsync();
        TempData["Ok"] = $"Đã gửi đơn nghỉ {soNgay} ngày, đang chờ duyệt.";
        return RedirectToAction(nameof(Index));
    }

    // Người duyệt: nhân sự duyệt tất cả, quản lý duyệt nhân viên của mình; không tự duyệt đơn của mình
    private async Task<(DonNghi Don, IActionResult Loi)> TaiDonDeDuyet(int id)
    {
        var don = await _db.DonNghis.Include(d => d.NguoiDung).FirstOrDefaultAsync(d => d.Id == id);
        if (don == null) return (null, NotFound());
        bool coQuyen = don.NguoiDungId != UserId &&
            (User.IsInRole(VaiTro.NhanSu) || (User.IsInRole(VaiTro.QuanLy) && don.NguoiDung.QuanLyId == UserId));
        if (!coQuyen) return (null, Forbid());
        if (don.TrangThai != TrangThaiDon.ChoDuyet)
        {
            TempData["Loi"] = "Đơn này đã được xử lý.";
            return (null, RedirectToAction(nameof(Index)));
        }
        return (don, null);
    }

    [HttpPost]
    public async Task<IActionResult> Duyet(int id)
    {
        var (don, loi) = await TaiDonDeDuyet(id);
        if (loi != null) return loi;

        don.TrangThai = TrangThaiDon.DaDuyet;
        don.NguoiDuyetId = UserId;
        don.NgayDuyet = DateTime.Now;
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Đã duyệt đơn.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> TuChoi(int id, string lyDo)
    {
        var (don, loi) = await TaiDonDeDuyet(id);
        if (loi != null) return loi;
        if (string.IsNullOrWhiteSpace(lyDo))
        {
            TempData["Loi"] = "Vui lòng nhập lý do từ chối.";
            return RedirectToAction(nameof(Index));
        }

        don.TrangThai = TrangThaiDon.TuChoi;
        don.NguoiDuyetId = UserId;
        don.NgayDuyet = DateTime.Now;
        don.LyDoTuChoi = lyDo.Trim();
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Đã từ chối đơn.";
        return RedirectToAction(nameof(Index));
    }

    // Tải giấy tờ đính kèm: chủ đơn, quản lý trực tiếp hoặc nhân sự
    public async Task<IActionResult> TepDinhKem(int id)
    {
        var don = await _db.DonNghis.Include(d => d.NguoiDung).FirstOrDefaultAsync(d => d.Id == id);
        if (don == null || string.IsNullOrEmpty(don.TepDinhKem)) return NotFound();

        bool coQuyen = don.NguoiDungId == UserId || User.IsInRole(VaiTro.NhanSu)
            || (User.IsInRole(VaiTro.QuanLy) && don.NguoiDung.QuanLyId == UserId);
        if (!coQuyen) return Forbid();

        var path = Path.Combine(_env.ContentRootPath, "App_Data", "don", don.TepDinhKem);
        if (!System.IO.File.Exists(path)) return NotFound();

        var duoi = Path.GetExtension(path).ToLowerInvariant();
        var mime = duoi == ".pdf" ? "application/pdf" : duoi == ".png" ? "image/png" : "image/jpeg";
        return PhysicalFile(path, mime, don.TenTepGoc);
    }
}
