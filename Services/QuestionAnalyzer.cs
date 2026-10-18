using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ChamCongAI.Services;

public static class Intents
{
    public const string ChaoHoi = "CHAO_HOI";
    public const string PhepConLai = "PHEP_CON_LAI";
    public const string ThongKe = "THONG_KE_CHAM_CONG";
    public const string TrangThaiDon = "TRANG_THAI_DON";
    public const string ChinhSach = "CHINH_SACH";
    public const string KetHop = "KET_HOP";
    public const string NgoaiPhamVi = "NGOAI_PHAM_VI";

    public static readonly string[] Tat =
        { ChaoHoi, PhepConLai, ThongKe, TrangThaiDon, ChinhSach, KetHop, NgoaiPhamVi };
}

public static class HamDbTen
{
    public const string PhepConLai = "PHEP_CON_LAI";
    public const string ThongKe = "THONG_KE_CHAM_CONG";
    public const string DanhSachDon = "DANH_SACH_DON";

    public static readonly string[] Tat = { PhepConLai, ThongKe, DanhSachDon };
}

public class ChatTurn
{
    public string VaiTro { get; set; }   // "user" hoặc "assistant"
    public string NoiDung { get; set; }
}

public class PhanTich
{
    public string Intent { get; set; }
    public string CauHoiVietLai { get; set; }
    public DateTime? TuNgay { get; set; }
    public DateTime? DenNgay { get; set; }
    public string LoaiNghi { get; set; }
    public string TenNhanVien { get; set; }
    public bool CanTaiLieu { get; set; }
    public List<string> HamDb { get; set; } = new();
    public string Nguon { get; set; }       // "LLM" hoặc "QUY_TAC"
    public string LoiLlm { get; set; }
    public long Ms { get; set; }
}

/// <summary>
/// Bước 1 của pipeline: hiểu câu hỏi.
/// - Ưu tiên dùng LLM để phân loại intent, trích xuất thực thể (thời gian, tên nhân viên, loại nghỉ)
///   và viết lại câu hỏi cho đầy đủ ngữ cảnh.
/// - Nếu LLM không có hoặc lỗi thì tự động chuyển sang bộ quy tắc từ khóa (fallback).
/// </summary>
public class QuestionAnalyzer
{
    private readonly LlmClient _llm;
    public QuestionAnalyzer(LlmClient llm) => _llm = llm;

    public async Task<PhanTich> PhanTichAsync(string cauHoi, List<ChatTurn> lichSu, bool dungLlm = true)
    {
        var sw = Stopwatch.StartNew();
        string loi = null;

        if (dungLlm && _llm.ChatSanSang)
        {
            try
            {
                var kq = await PhanTichBangLlmAsync(cauHoi, lichSu);
                kq.Ms = sw.ElapsedMilliseconds;
                return kq;
            }
            catch (Exception ex)
            {
                loi = ex.Message;
            }
        }

        var qt = PhanTichQuyTac(cauHoi);
        qt.LoiLlm = loi ?? (dungLlm && !_llm.ChatSanSang ? "Chưa cấu hình API key cho mô hình chat" : null);
        qt.Ms = sw.ElapsedMilliseconds;
        return qt;
    }

    // ---------------------------------------------------------------- LLM
    private async Task<PhanTich> PhanTichBangLlmAsync(string cauHoi, List<ChatTurn> lichSu)
    {
        var hn = DateTime.Today;
        var he = new StringBuilder();
        he.AppendLine("Bạn là bộ phân tích câu hỏi của trợ lý nhân sự (chấm công, nghỉ phép). " +
                      $"Hôm nay là {hn:dd/MM/yyyy} ({hn.DayOfWeek}). " +
                      "Chỉ trả về MỘT đối tượng JSON, không giải thích, không dùng markdown.");
        he.AppendLine("Các intent hợp lệ:");
        he.AppendLine("- CHAO_HOI: chào hỏi, cảm ơn, hỏi bạn là ai.");
        he.AppendLine("- PHEP_CON_LAI: hỏi số ngày phép năm còn lại hoặc đã dùng của người dùng.");
        he.AppendLine("- THONG_KE_CHAM_CONG: hỏi ngày công, đi trễ, về sớm, vắng mặt, giờ vào/ra của người dùng.");
        he.AppendLine("- TRANG_THAI_DON: hỏi về các đơn xin nghỉ đã gửi (đã duyệt chưa, bị từ chối không...).");
        he.AppendLine("- CHINH_SACH: hỏi quy định, chế độ chung (thai sản, hiếu hỉ, nghỉ cưới, nghỉ ốm, quy trình xin nghỉ, giờ làm, phép năm được bao nhiêu ngày...).");
        he.AppendLine("- KET_HOP: cần cả dữ liệu cá nhân lẫn quy định để trả lời.");
        he.AppendLine("- NGOAI_PHAM_VI: không liên quan nhân sự, chấm công, nghỉ phép.");
        he.AppendLine("Định dạng JSON:");
        he.AppendLine("{\"intent\":\"...\",\"cau_hoi_viet_lai\":\"...\",\"tu_ngay\":\"yyyy-MM-dd hoặc null\",\"den_ngay\":\"yyyy-MM-dd hoặc null\"," +
                      "\"ten_nhan_vien\":\"... hoặc null\",\"loai_nghi\":\"phep_nam|om|thai_san|ket_hon|hieu_hi|khong_luong|null\"," +
                      "\"can_tai_lieu\":true,\"ham_db\":[\"PHEP_CON_LAI\",\"THONG_KE_CHAM_CONG\",\"DANH_SACH_DON\"]}");
        he.AppendLine("Quy tắc:");
        he.AppendLine("- cau_hoi_viet_lai: viết lại thành câu hỏi đầy đủ, tự đứng được, thay đại từ hoặc phần ngầm hiểu bằng nội dung từ lịch sử hội thoại.");
        he.AppendLine("- Quy đổi 'hôm nay, hôm qua, tuần này, tháng này, tháng trước...' thành khoảng ngày cụ thể. Câu hỏi chấm công không nói thời gian thì dùng tháng hiện tại.");
        he.AppendLine("- ten_nhan_vien: chỉ điền khi người dùng hỏi về một nhân viên khác; hỏi về chính mình thì null.");
        he.AppendLine("- ham_db: chỉ liệt kê hàm dữ liệu thật sự cần (có thể rỗng). can_tai_lieu = true khi cần tra quy định trong tài liệu.");

        var user = new StringBuilder();
        if (lichSu.Count > 0)
        {
            user.AppendLine("Lịch sử hội thoại gần đây:");
            foreach (var t in lichSu.TakeLast(4))
                user.AppendLine($"- {(t.VaiTro == "assistant" ? "Trợ lý" : "Người dùng")}: {VanBan.Cat(t.NoiDung, 300)}");
            user.AppendLine();
        }
        user.AppendLine("Câu hỏi hiện tại: " + cauHoi);

        var raw = await _llm.ChatAsync(new[]
        {
            new ChatMsg("system", he.ToString()),
            new ChatMsg("user", user.ToString())
        }, 0.0);

        return DocJson(raw, cauHoi);
    }

    private static PhanTich DocJson(string raw, string cauHoi)
    {
        int a = raw.IndexOf('{'), b = raw.LastIndexOf('}');
        if (a < 0 || b <= a) throw new FormatException("Mô hình không trả về JSON hợp lệ.");

        using var doc = JsonDocument.Parse(raw.Substring(a, b - a + 1));
        var r = doc.RootElement;

        string S(string ten)
        {
            if (r.TryGetProperty(ten, out var e) && e.ValueKind == JsonValueKind.String)
            {
                var v = e.GetString()?.Trim();
                return string.IsNullOrEmpty(v) || v.Equals("null", StringComparison.OrdinalIgnoreCase) ? null : v;
            }
            return null;
        }

        DateTime? Ngay(string s) =>
            DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

        var pt = new PhanTich { Nguon = "LLM" };
        pt.Intent = (S("intent") ?? "").ToUpperInvariant();
        if (!Intents.Tat.Contains(pt.Intent))
            throw new FormatException("Intent không hợp lệ: " + pt.Intent);

        pt.CauHoiVietLai = S("cau_hoi_viet_lai") ?? cauHoi;
        pt.TuNgay = Ngay(S("tu_ngay"));
        pt.DenNgay = Ngay(S("den_ngay"));
        if (pt.TuNgay > pt.DenNgay) (pt.TuNgay, pt.DenNgay) = (pt.DenNgay, pt.TuNgay);
        pt.TenNhanVien = S("ten_nhan_vien");
        pt.LoaiNghi = S("loai_nghi");
        pt.CanTaiLieu = r.TryGetProperty("can_tai_lieu", out var ct) && ct.ValueKind == JsonValueKind.True;

        if (r.TryGetProperty("ham_db", out var hd) && hd.ValueKind == JsonValueKind.Array)
            foreach (var e in hd.EnumerateArray())
                if (e.ValueKind == JsonValueKind.String && HamDbTen.Tat.Contains(e.GetString()) && !pt.HamDb.Contains(e.GetString()))
                    pt.HamDb.Add(e.GetString());

        return pt;
    }

    // ----------------------------------------------------------- Quy tắc
    public PhanTich PhanTichQuyTac(string cauHoi)
    {
        var k = VanBan.BoDau(cauHoi);
        bool Co(params string[] ts) => ts.Any(t => k.Contains(t));

        bool phep = Co("phep") && Co("con lai", "con bao nhieu", "con may", "da dung", "con duoc");
        bool chamCong = Co("di tre", "ve som", "cham cong", "ngay cong", "vang", "di muon", "gio vao", "gio ra");
        bool don = Co("don") && Co("trang thai", "duyet", "da gui", "xin nghi", "cua toi");
        bool chinhSach = Co("quy dinh", "chinh sach", "che do", "thai san", "hieu", "cuoi", "ket hon", "tang le",
            "quy trinh", "thu tuc", "bao lau", "duoc nghi", "giay to", "ho so", "dieu kien", "phep nam",
            "xu ly", "phat", "bao luu", "kham thai");
        bool chao = k.Length < 30 && Co("xin chao", "chao ban", "hello", "cam on", "tam biet");

        var pt = new PhanTich { Nguon = "QUY_TAC", CauHoiVietLai = cauHoi };
        bool coDuLieu = phep || chamCong || don;

        if (coDuLieu && chinhSach) pt.Intent = Intents.KetHop;
        else if (phep) pt.Intent = Intents.PhepConLai;
        else if (chamCong) pt.Intent = Intents.ThongKe;
        else if (don) pt.Intent = Intents.TrangThaiDon;
        else if (chinhSach) pt.Intent = Intents.ChinhSach;
        else if (chao) pt.Intent = Intents.ChaoHoi;
        else if (Co("nghi")) pt.Intent = Intents.ChinhSach;
        else pt.Intent = Intents.NgoaiPhamVi;

        if (phep) pt.HamDb.Add(HamDbTen.PhepConLai);
        if (chamCong) pt.HamDb.Add(HamDbTen.ThongKe);
        if (don) pt.HamDb.Add(HamDbTen.DanhSachDon);
        pt.CanTaiLieu = pt.Intent == Intents.ChinhSach || pt.Intent == Intents.KetHop;

        GanThoiGian(k, pt);
        return pt;
    }

    private static void GanThoiGian(string k, PhanTich pt)
    {
        var hn = DateTime.Today;
        int lui = ((int)hn.DayOfWeek + 6) % 7; // số ngày kể từ thứ Hai
        if (k.Contains("thang truoc"))
        {
            var dau = new DateTime(hn.Year, hn.Month, 1).AddMonths(-1);
            pt.TuNgay = dau; pt.DenNgay = dau.AddMonths(1).AddDays(-1);
        }
        else if (k.Contains("tuan truoc"))
        {
            pt.TuNgay = hn.AddDays(-lui - 7); pt.DenNgay = pt.TuNgay.Value.AddDays(6);
        }
        else if (k.Contains("tuan nay"))
        {
            pt.TuNgay = hn.AddDays(-lui); pt.DenNgay = hn;
        }
        else if (k.Contains("hom qua"))
        {
            pt.TuNgay = hn.AddDays(-1); pt.DenNgay = hn.AddDays(-1);
        }
        else if (k.Contains("hom nay"))
        {
            pt.TuNgay = hn; pt.DenNgay = hn;
        }
        else
        {
            pt.TuNgay = new DateTime(hn.Year, hn.Month, 1); pt.DenNgay = hn;
        }
    }
}
