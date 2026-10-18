using System.Text;
using System.Text.Json;
using ChamCongAI.Data;
using ChamCongAI.Models;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;

namespace ChamCongAI.Services;

/// <summary>Nạp tài liệu chính sách: đọc văn bản -> chia đoạn -> (tùy chọn) tạo embedding -> lưu DB.</summary>
public class TaiLieuService
{
    private readonly AppDbContext _db;
    private readonly LlmClient _llm;

    public TaiLieuService(AppDbContext db, LlmClient llm)
    {
        _db = db;
        _llm = llm;
    }

    public static string DocVanBan(Stream stream, string tenFile)
    {
        var duoi = Path.GetExtension(tenFile).ToLowerInvariant();
        if (duoi == ".pdf")
        {
            var sb = new StringBuilder();
            using var pdf = PdfDocument.Open(stream);
            foreach (var page in pdf.GetPages())
                sb.AppendLine(page.Text).AppendLine();
            return sb.ToString();
        }
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public async Task<(TaiLieu TaiLieu, int SoDoan)> NapAsync(string tieuDe, string tenFile, string vanBan)
    {
        var tl = new TaiLieu { TieuDe = tieuDe, TenFile = tenFile };
        int stt = 1;
        foreach (var d in VanBan.ChiaDoan(vanBan))
            tl.Doans.Add(new TaiLieuDoan { ThuTu = stt++, NoiDung = d });

        _db.TaiLieus.Add(tl);
        await _db.SaveChangesAsync();
        return (tl, tl.Doans.Count);
    }

    /// <summary>Tạo embedding cho các đoạn chưa có. Trả về (số đoạn thành công, thông báo lỗi nếu có).</summary>
    public async Task<(int ThanhCong, string Loi)> TaoEmbeddingAsync(int toiDa = 300)
    {
        if (!_llm.EmbeddingSanSang) return (0, "Chưa cấu hình API key cho embedding.");

        var cho = await _db.TaiLieuDoans.Where(d => d.EmbeddingJson == null).Take(toiDa).ToListAsync();
        int ok = 0;
        try
        {
            foreach (var d in cho)
            {
                var v = await _llm.EmbedAsync(d.NoiDung);
                d.EmbeddingJson = JsonSerializer.Serialize(v);
                ok++;
                await Task.Delay(200);
            }
        }
        catch (Exception ex)
        {
            await _db.SaveChangesAsync();
            return (ok, ex.Message);
        }
        await _db.SaveChangesAsync();
        return (ok, null);
    }
}
