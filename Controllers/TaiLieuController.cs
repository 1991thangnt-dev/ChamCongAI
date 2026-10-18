using ChamCongAI.Data;
using ChamCongAI.Models;
using ChamCongAI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChamCongAI.Controllers;

[Authorize(Roles = VaiTro.NhanSu)]
public class TaiLieuController : Controller
{
    private static readonly string[] DuoiChoPhep = { ".txt", ".md", ".pdf" };

    private readonly AppDbContext _db;
    private readonly TaiLieuService _svc;
    private readonly LlmClient _llm;

    public TaiLieuController(AppDbContext db, TaiLieuService svc, LlmClient llm)
    {
        _db = db; _svc = svc; _llm = llm;
    }

    public async Task<IActionResult> Index()
    {
        var ds = await _db.TaiLieus
            .Select(t => new TaiLieuHang
            {
                Id = t.Id, TieuDe = t.TieuDe, TenFile = t.TenFile, NgayTai = t.NgayTai,
                SoDoan = t.Doans.Count, SoEmbedding = t.Doans.Count(d => d.EmbeddingJson != null)
            })
            .OrderBy(t => t.TieuDe).ToListAsync();
        ViewBag.EmbSan = _llm.EmbeddingSanSang;
        return View(ds);
    }

    [HttpPost]
    public async Task<IActionResult> Upload(IFormFile tep, string tieuDe)
    {
        if (tep == null || tep.Length == 0)
        {
            TempData["Loi"] = "Vui lòng chọn file tài liệu.";
            return RedirectToAction(nameof(Index));
        }
        var duoi = Path.GetExtension(tep.FileName).ToLowerInvariant();
        if (!DuoiChoPhep.Contains(duoi) || tep.Length > 10 * 1024 * 1024)
        {
            TempData["Loi"] = "Chỉ nhận file .txt, .md, .pdf tối đa 10 MB.";
            return RedirectToAction(nameof(Index));
        }

        string vanBan;
        try
        {
            await using var s = tep.OpenReadStream();
            vanBan = TaiLieuService.DocVanBan(s, tep.FileName);
        }
        catch (Exception ex)
        {
            TempData["Loi"] = "Không đọc được nội dung file: " + ex.Message;
            return RedirectToAction(nameof(Index));
        }
        if (string.IsNullOrWhiteSpace(vanBan))
        {
            TempData["Loi"] = "File không có nội dung văn bản (PDF dạng ảnh scan cần OCR trước).";
            return RedirectToAction(nameof(Index));
        }

        var ten = string.IsNullOrWhiteSpace(tieuDe) ? Path.GetFileNameWithoutExtension(tep.FileName) : tieuDe.Trim();
        var (_, soDoan) = await _svc.NapAsync(ten, tep.FileName, vanBan);

        string tinNhan = $"Đã nạp \"{ten}\" ({soDoan} đoạn).";
        if (_llm.EmbeddingSanSang)
        {
            var (ok, loi) = await _svc.TaoEmbeddingAsync();
            tinNhan += loi == null ? $" Đã tạo embedding cho {ok} đoạn." : $" Tạo embedding dừng ở đoạn {ok}: {loi}";
        }
        TempData["Ok"] = tinNhan;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> TaoEmbedding()
    {
        var (ok, loi) = await _svc.TaoEmbeddingAsync();
        if (loi == null) TempData["Ok"] = $"Đã tạo embedding cho {ok} đoạn.";
        else TempData["Loi"] = $"Tạo được {ok} đoạn, sau đó lỗi: {loi}";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Xem(int id)
    {
        var tl = await _db.TaiLieus.Include(t => t.Doans.OrderBy(d => d.ThuTu)).FirstOrDefaultAsync(t => t.Id == id);
        if (tl == null) return NotFound();
        return View(tl);
    }

    [HttpPost]
    public async Task<IActionResult> Xoa(int id)
    {
        var tl = await _db.TaiLieus.FindAsync(id);
        if (tl != null)
        {
            _db.TaiLieus.Remove(tl);
            await _db.SaveChangesAsync();
            TempData["Ok"] = "Đã xóa tài liệu.";
        }
        return RedirectToAction(nameof(Index));
    }
}

public class TaiLieuHang
{
    public int Id { get; set; }
    public string TieuDe { get; set; }
    public string TenFile { get; set; }
    public DateTime NgayTai { get; set; }
    public int SoDoan { get; set; }
    public int SoEmbedding { get; set; }
}
