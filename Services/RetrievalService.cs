using System.Text.Json;
using ChamCongAI.Data;
using ChamCongAI.Models;
using Microsoft.EntityFrameworkCore;

namespace ChamCongAI.Services;

public class KetQuaTim
{
    public TaiLieuDoan Doan { get; set; }
    public double Bm25 { get; set; }
    public double Cosine { get; set; }
    public int HangBm25 { get; set; } = int.MaxValue;
    public int HangVector { get; set; } = int.MaxValue;
    public double Rrf { get; set; }
}

/// <summary>
/// Truy xuất lai (hybrid retrieval): BM25 (khớp từ khóa) + embedding (khớp ngữ nghĩa),
/// hợp nhất bằng Reciprocal Rank Fusion (RRF). Nếu chưa có embedding thì tự động dùng riêng BM25.
/// </summary>
public class RetrievalService
{
    private const double K1 = 1.5, B = 0.75;
    private const int HangRrf = 60;

    private readonly AppDbContext _db;
    private readonly LlmClient _llm;

    public RetrievalService(AppDbContext db, LlmClient llm)
    {
        _db = db;
        _llm = llm;
    }

    public async Task<(List<KetQuaTim> Ket, string GhiChu)> TimAsync(string cauHoi, int k = 4, bool dungVector = true)
    {
        var doans = await _db.TaiLieuDoans.Include(d => d.TaiLieu).AsNoTracking().ToListAsync();
        if (doans.Count == 0) return (new List<KetQuaTim>(), "Kho tài liệu đang trống.");

        var ket = doans.Select(d => new KetQuaTim { Doan = d }).ToList();

        // ---- BM25 ----
        var qTokens = VanBan.Tokens(cauHoi).Distinct().ToList();
        var docTokens = doans.Select(d => VanBan.Tokens(d.NoiDung)).ToList();
        double avg = Math.Max(1, docTokens.Average(t => t.Count));
        int n = doans.Count;
        var df = new Dictionary<string, int>();
        foreach (var toks in docTokens)
            foreach (var w in toks.Distinct())
                df[w] = df.GetValueOrDefault(w) + 1;

        for (int i = 0; i < n; i++)
        {
            var tf = docTokens[i].GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
            double s = 0;
            foreach (var q in qTokens)
            {
                if (!tf.TryGetValue(q, out var f)) continue;
                double idf = Math.Log(1 + (n - df[q] + 0.5) / (df[q] + 0.5));
                s += idf * f * (K1 + 1) / (f + K1 * (1 - B + B * docTokens[i].Count / avg));
            }
            ket[i].Bm25 = s;
        }
        int hang = 1;
        foreach (var r in ket.Where(x => x.Bm25 > 0).OrderByDescending(x => x.Bm25))
            r.HangBm25 = hang++;

        // ---- Vector (nếu có) ----
        string ghiChu = "BM25";
        if (dungVector && _llm.EmbeddingSanSang && doans.Any(d => d.EmbeddingJson != null))
        {
            try
            {
                var qv = await _llm.EmbedAsync(cauHoi);
                foreach (var r in ket.Where(x => x.Doan.EmbeddingJson != null))
                {
                    var dv = JsonSerializer.Deserialize<float[]>(r.Doan.EmbeddingJson);
                    r.Cosine = VanBan.Cosine(qv, dv);
                }
                hang = 1;
                foreach (var r in ket.Where(x => x.Doan.EmbeddingJson != null).OrderByDescending(x => x.Cosine))
                    r.HangVector = hang++;
                ghiChu = "BM25 + embedding (hợp nhất RRF)";
            }
            catch (Exception ex)
            {
                ghiChu = "Chỉ BM25 (embedding lỗi: " + VanBan.Cat(ex.Message, 120) + ")";
            }
        }
        else if (dungVector && !_llm.EmbeddingSanSang)
        {
            ghiChu = "Chỉ BM25 (chưa cấu hình embedding)";
        }
        else if (dungVector)
        {
            ghiChu = "Chỉ BM25 (tài liệu chưa có embedding, hãy bấm 'Tạo embedding' ở trang Tài liệu)";
        }

        // ---- Hợp nhất RRF ----
        foreach (var r in ket)
        {
            double s = 0;
            if (r.HangBm25 != int.MaxValue) s += 1.0 / (HangRrf + r.HangBm25);
            if (r.HangVector != int.MaxValue) s += 1.0 / (HangRrf + r.HangVector);
            r.Rrf = s;
        }

        var top = ket.Where(x => x.Rrf > 0).OrderByDescending(x => x.Rrf).Take(k).ToList();
        return (top, ghiChu);
    }
}
