using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ChamCongAI.Services;

/// <summary>Tiện ích xử lý văn bản tiếng Việt: bỏ dấu, tách từ, chia đoạn, độ tương đồng cosine.</summary>
public static class VanBan
{
    private static readonly HashSet<string> TuDung = new()
    {
        "la","va","cua","co","khong","toi","bao","nhieu","nhu","the","nao","duoc","cho","thi",
        "bi","o","ve","voi","cac","nhung","mot","hay","khi","de","nay","do","gi","may","ra","minh"
    };

    public static string BoDau(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC)
            .Replace('đ', 'd').Replace('Đ', 'D').ToLowerInvariant();
    }

    /// <summary>Tách thành các từ không dấu (bỏ từ dừng) + cặp từ liền kề (bigram) để khớp cụm từ.</summary>
    public static List<string> Tokens(string s)
    {
        var raw = Regex.Split(BoDau(s), @"[^a-z0-9]+").Where(t => t.Length > 0).ToList();
        var res = new List<string>();
        foreach (var t in raw)
            if (!TuDung.Contains(t)) res.Add(t);
        for (int i = 0; i + 1 < raw.Count; i++)
            res.Add(raw[i] + "_" + raw[i + 1]);
        return res;
    }

    /// <summary>Chia văn bản thành các đoạn (chunk) theo đoạn văn, tối đa khoảng toiDa ký tự.</summary>
    public static List<string> ChiaDoan(string text, int toiDa = 600)
    {
        var kq = new List<string>();
        var cur = new StringBuilder();

        void Chot()
        {
            var t = cur.ToString().Trim();
            if (t.Length > 0) kq.Add(t);
            cur.Clear();
        }

        var paras = Regex.Split((text ?? "").Replace("\r", ""), @"\n\s*\n")
            .Select(p => p.Trim()).Where(p => p.Length > 0);

        foreach (var p in paras)
        {
            if (cur.Length > 0 && cur.Length + p.Length + 2 > toiDa) Chot();

            if (p.Length > toiDa)
            {
                foreach (var cau in Regex.Split(p, @"(?<=[\.\!\?;])\s+"))
                {
                    if (cur.Length > 0 && cur.Length + cau.Length + 1 > toiDa) Chot();
                    cur.Append(cau).Append(' ');
                }
                cur.Append("\n\n");
            }
            else
            {
                cur.Append(p).Append("\n\n");
            }
        }
        Chot();
        return kq;
    }

    public static double Cosine(float[] a, float[] b)
    {
        if (a == null || b == null || a.Length != b.Length || a.Length == 0) return 0;
        double dot = 0, na = 0, nb = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }
        return (na == 0 || nb == 0) ? 0 : dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }

    public static string Cat(string s, int n)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace("\r", " ").Replace("\n", " ");
        return s.Length <= n ? s : s[..n] + "...";
    }
}

/// <summary>Lịch làm việc chuẩn của công ty (khớp với tài liệu Quy-dinh-cham-cong).</summary>
public static class LichLam
{
    public static readonly TimeSpan GioVao = new(8, 0, 0);
    public static readonly TimeSpan GioRa = new(17, 0, 0);
    public const int PhutChoPhep = 5;

    public static bool LaNgayLam(DateTime d) =>
        d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday;

    public static int DemNgayLam(DateTime tu, DateTime den)
    {
        int n = 0;
        for (var d = tu.Date; d <= den.Date; d = d.AddDays(1))
            if (LaNgayLam(d)) n++;
        return n;
    }

    public static (int DiTre, int VeSom) TinhTreSom(TimeSpan? vao, TimeSpan? ra)
    {
        int tre = 0, som = 0;
        if (vao.HasValue && vao.Value > GioVao.Add(TimeSpan.FromMinutes(PhutChoPhep)))
            tre = (int)(vao.Value - GioVao).TotalMinutes;
        if (ra.HasValue && ra.Value < GioRa)
            som = (int)(GioRa - ra.Value).TotalMinutes;
        return (tre, som);
    }
}
