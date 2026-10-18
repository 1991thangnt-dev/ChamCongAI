using System.Text.Json;
using ChamCongAI.Models;

namespace ChamCongAI.Services;

public class CaDanhGia
{
    public string Q { get; set; }
    public string Intent { get; set; }
    public bool CaTaiLieu { get; set; }
    public List<string> HamDb { get; set; } = new();
    public string TuKhoa { get; set; }
}

public class DongDanhGia
{
    public string Cau { get; set; }
    public string IntentKyVong { get; set; }
    public string IntentQuyTac { get; set; }
    public bool QuyTacDungIntent { get; set; }
    public bool QuyTacDungRouter { get; set; }
    public string IntentLlm { get; set; }
    public bool? LlmDungIntent { get; set; }
    public bool? LlmDungRouter { get; set; }
    public bool? HitBm25 { get; set; }
    public bool? HitHybrid { get; set; }
}

public class BaoCaoDanhGia
{
    public List<DongDanhGia> Dong { get; set; } = new();
    public bool DungLlm { get; set; }
    public bool CoEmbedding { get; set; }

    public static string Pct(int a, int b) => b == 0 ? "-" : $"{a * 100.0 / b:0.#}% ({a}/{b})";

    public string IntentQuyTac => Pct(Dong.Count(d => d.QuyTacDungIntent), Dong.Count);
    public string RouterQuyTac => Pct(Dong.Count(d => d.QuyTacDungRouter), Dong.Count);
    public string IntentLlm => DungLlm ? Pct(Dong.Count(d => d.LlmDungIntent == true), Dong.Count) : "-";
    public string RouterLlm => DungLlm ? Pct(Dong.Count(d => d.LlmDungRouter == true), Dong.Count) : "-";
    public string HitBm25 => Pct(Dong.Count(d => d.HitBm25 == true), Dong.Count(d => d.HitBm25 != null));
    public string HitHybrid => CoEmbedding ? Pct(Dong.Count(d => d.HitHybrid == true), Dong.Count(d => d.HitHybrid != null)) : "-";
}

/// <summary>
/// Chạy bộ câu hỏi mẫu (Eval/cauhoi_mau.json) để đo chất lượng: so sánh bộ quy tắc với LLM
/// (phân loại intent + định tuyến) và so sánh BM25 với hybrid (truy xuất đúng đoạn tài liệu trong top 3).
/// </summary>
public class DanhGiaService
{
    private readonly IWebHostEnvironment _env;
    private readonly QuestionAnalyzer _analyzer;
    private readonly RetrievalService _retrieval;
    private readonly LlmClient _llm;

    public DanhGiaService(IWebHostEnvironment env, QuestionAnalyzer analyzer, RetrievalService retrieval, LlmClient llm)
    {
        _env = env; _analyzer = analyzer; _retrieval = retrieval; _llm = llm;
    }

    public async Task<BaoCaoDanhGia> ChayAsync(bool dungLlm)
    {
        var path = Path.Combine(_env.ContentRootPath, "Eval", "cauhoi_mau.json");
        var cases = JsonSerializer.Deserialize<List<CaDanhGia>>(await File.ReadAllTextAsync(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

        var bc = new BaoCaoDanhGia { DungLlm = dungLlm && _llm.ChatSanSang, CoEmbedding = _llm.EmbeddingSanSang };
        var rong = new List<ChatTurn>();

        bool RouterDung(PhanTich pt, CaDanhGia c)
        {
            var (db, rag) = ChatOrchestrator.DinhTuyen(pt);
            return rag == c.CaTaiLieu && new HashSet<string>(db).SetEquals(c.HamDb ?? new List<string>());
        }

        foreach (var c in cases)
        {
            var row = new DongDanhGia { Cau = c.Q, IntentKyVong = c.Intent };

            var qt = _analyzer.PhanTichQuyTac(c.Q);
            row.IntentQuyTac = qt.Intent;
            row.QuyTacDungIntent = qt.Intent == c.Intent;
            row.QuyTacDungRouter = RouterDung(qt, c);

            if (bc.DungLlm)
            {
                var pl = await _analyzer.PhanTichAsync(c.Q, rong, true);
                bool tuLlm = pl.Nguon == "LLM";
                row.IntentLlm = tuLlm ? pl.Intent : "(LLM lỗi)";
                row.LlmDungIntent = tuLlm && pl.Intent == c.Intent;
                row.LlmDungRouter = tuLlm && RouterDung(pl, c);
                await Task.Delay(800);
            }

            if (!string.IsNullOrEmpty(c.TuKhoa))
            {
                var k = VanBan.BoDau(c.TuKhoa);
                bool Hit(List<KetQuaTim> l) => l.Take(3).Any(x => VanBan.BoDau(x.Doan.NoiDung).Contains(k));

                row.HitBm25 = Hit((await _retrieval.TimAsync(c.Q, 3, false)).Ket);
                if (bc.CoEmbedding)
                {
                    row.HitHybrid = Hit((await _retrieval.TimAsync(c.Q, 3, true)).Ket);
                    await Task.Delay(300);
                }
            }
            bc.Dong.Add(row);
        }
        return bc;
    }
}
