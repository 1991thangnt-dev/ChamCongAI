using System.Text.Encodings.Web;
using System.Text.Json;
using ChamCongAI.Data;
using ChamCongAI.Models;
using Microsoft.EntityFrameworkCore;

namespace ChamCongAI.Services;

public class PhepConLaiDto
{
    public int Nam { get; set; }
    public int SoNgayPhepDuocCap { get; set; }
    public int SoNgayDaDung { get; set; }
    public int SoNgayDangChoDuyet { get; set; }
    public int SoNgayConLai { get; set; }
    public int SoNgayConLaiSauKhiTruDonDangCho { get; set; }
}

public class ChiTietTreDto
{
    public string Ngay { get; set; }
    public string GioVao { get; set; }
    public int PhutTre { get; set; }
}

public class ThongKeChamCongDto
{
    public string TuNgay { get; set; }
    public string DenNgay { get; set; }
    public int SoNgayLamTheoLich { get; set; }
    public int SoNgayCoMat { get; set; }
    public int SoNgayNghiCoPhep { get; set; }
    public int SoNgayVangKhongPhep { get; set; }
    public int SoLanDiTre { get; set; }
    public int TongPhutDiTre { get; set; }
    public int SoLanVeSom { get; set; }
    public int TongPhutVeSom { get; set; }
    public List<ChiTietTreDto> ChiTietDiTre { get; set; } = new();
    public List<string> NgayVangKhongPhep { get; set; } = new();
}

public class DonNghiDto
{
    public string Loai { get; set; }
    public string TuNgay { get; set; }
    public string DenNgay { get; set; }
    public int SoNgay { get; set; }
    public string TrangThai { get; set; }
    public string LyDoTuChoi { get; set; }
}

/// <summary>
/// Các "hàm công cụ" (function calling) mà chatbot được phép gọi để lấy dữ liệu thật từ database.
/// Mô hình AI không bao giờ đọc DB trực tiếp và không tự tính số liệu: mọi con số do code ở đây tính.
/// </summary>
public class ChatTools
{
    public static readonly JsonSerializerOptions JsonOpt = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly AppDbContext _db;
    public ChatTools(AppDbContext db) => _db = db;

    public async Task<PhepConLaiDto> PhepConLaiAsync(NguoiDung nv, int nam)
    {
        var dons = await _db.DonNghis
            .Where(d => d.NguoiDungId == nv.Id && d.Loai == LoaiNghi.PhepNam
                        && d.TuNgay.Year == nam && d.TrangThai != TrangThaiDon.TuChoi)
            .ToListAsync();

        int da = dons.Where(d => d.TrangThai == TrangThaiDon.DaDuyet).Sum(d => d.SoNgay);
        int cho = dons.Where(d => d.TrangThai == TrangThaiDon.ChoDuyet).Sum(d => d.SoNgay);
        return new PhepConLaiDto
        {
            Nam = nam,
            SoNgayPhepDuocCap = nv.PhepNamDuocCap,
            SoNgayDaDung = da,
            SoNgayDangChoDuyet = cho,
            SoNgayConLai = nv.PhepNamDuocCap - da,
            SoNgayConLaiSauKhiTruDonDangCho = nv.PhepNamDuocCap - da - cho
        };
    }

    public async Task<ThongKeChamCongDto> ThongKeChamCongAsync(NguoiDung nv, DateTime tu, DateTime den)
    {
        tu = tu.Date;
        den = den.Date;
        var homNay = DateTime.Today;
        if (den > homNay) den = homNay;

        var kq = new ThongKeChamCongDto { TuNgay = tu.ToString("dd/MM/yyyy"), DenNgay = den.ToString("dd/MM/yyyy") };
        if (tu > den) return kq;

        var cc = await _db.ChamCongs
            .Where(x => x.NguoiDungId == nv.Id && x.Ngay >= tu && x.Ngay <= den)
            .OrderBy(x => x.Ngay).ToListAsync();
        var don = await _db.DonNghis
            .Where(d => d.NguoiDungId == nv.Id && d.TrangThai == TrangThaiDon.DaDuyet
                        && d.TuNgay <= den && d.DenNgay >= tu)
            .ToListAsync();

        var coMat = cc.Where(c => c.GioVao != null).Select(c => c.Ngay.Date).ToHashSet();

        for (var d = tu; d <= den; d = d.AddDays(1))
        {
            if (!LichLam.LaNgayLam(d)) continue;
            kq.SoNgayLamTheoLich++;
            if (don.Any(x => x.TuNgay.Date <= d && x.DenNgay.Date >= d)) { kq.SoNgayNghiCoPhep++; continue; }
            if (!coMat.Contains(d) && d != homNay)
            {
                kq.SoNgayVangKhongPhep++;
                if (kq.NgayVangKhongPhep.Count < 10) kq.NgayVangKhongPhep.Add(d.ToString("dd/MM/yyyy"));
            }
        }

        kq.SoNgayCoMat = coMat.Count;
        foreach (var c in cc)
        {
            if (c.DiTrePhut > 0)
            {
                kq.SoLanDiTre++;
                kq.TongPhutDiTre += c.DiTrePhut;
                if (kq.ChiTietDiTre.Count < 10)
                    kq.ChiTietDiTre.Add(new ChiTietTreDto
                    {
                        Ngay = c.Ngay.ToString("dd/MM/yyyy"),
                        GioVao = c.GioVao?.ToString(@"hh\:mm"),
                        PhutTre = c.DiTrePhut
                    });
            }
            if (c.VeSomPhut > 0)
            {
                kq.SoLanVeSom++;
                kq.TongPhutVeSom += c.VeSomPhut;
            }
        }
        return kq;
    }

    public async Task<List<DonNghiDto>> DanhSachDonAsync(NguoiDung nv, DateTime? tu, DateTime? den)
    {
        var q = _db.DonNghis.Where(d => d.NguoiDungId == nv.Id);
        if (tu.HasValue) q = q.Where(d => d.DenNgay >= tu.Value.Date);
        if (den.HasValue) q = q.Where(d => d.TuNgay <= den.Value.Date);

        var ds = await q.OrderByDescending(d => d.NgayTao).Take(10).ToListAsync();
        return ds.Select(d => new DonNghiDto
        {
            Loai = d.Loai.Ten(),
            TuNgay = d.TuNgay.ToString("dd/MM/yyyy"),
            DenNgay = d.DenNgay.ToString("dd/MM/yyyy"),
            SoNgay = d.SoNgay,
            TrangThai = d.TrangThai.Ten(),
            LyDoTuChoi = d.LyDoTuChoi
        }).ToList();
    }
}
