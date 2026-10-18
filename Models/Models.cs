using System.ComponentModel.DataAnnotations.Schema;

namespace ChamCongAI.Models;

public static class VaiTro
{
    public const string NhanVien = "NhanVien";
    public const string QuanLy = "QuanLy";
    public const string NhanSu = "NhanSu";

    public static string Ten(string v) => v switch
    {
        NhanVien => "Nhân viên",
        QuanLy => "Quản lý",
        NhanSu => "Nhân sự",
        _ => v
    };
}

public class NguoiDung
{
    public int Id { get; set; }
    public string TenDangNhap { get; set; }
    public string MatKhauHash { get; set; }
    public string HoTen { get; set; }
    public string PhongBan { get; set; }
    public string VaiTro { get; set; }

    public int? QuanLyId { get; set; }
    public NguoiDung QuanLy { get; set; }

    public DateTime NgayVaoLam { get; set; }
    public int PhepNamDuocCap { get; set; } = 12;
}

public class ChamCong
{
    public int Id { get; set; }
    public int NguoiDungId { get; set; }
    public NguoiDung NguoiDung { get; set; }

    [Column(TypeName = "date")]
    public DateTime Ngay { get; set; }
    public TimeSpan? GioVao { get; set; }
    public TimeSpan? GioRa { get; set; }
    public int DiTrePhut { get; set; }
    public int VeSomPhut { get; set; }
}

public enum LoaiNghi { PhepNam, Om, ThaiSan, KetHon, HieuHi, KhongLuong }
public enum TrangThaiDon { ChoDuyet, DaDuyet, TuChoi }

public static class NghiExt
{
    public static string Ten(this LoaiNghi l) => l switch
    {
        LoaiNghi.PhepNam => "Phép năm",
        LoaiNghi.Om => "Nghỉ ốm",
        LoaiNghi.ThaiSan => "Thai sản",
        LoaiNghi.KetHon => "Kết hôn",
        LoaiNghi.HieuHi => "Hiếu hỉ (tang lễ...)",
        LoaiNghi.KhongLuong => "Không lương",
        _ => l.ToString()
    };

    public static string Ten(this TrangThaiDon t) => t switch
    {
        TrangThaiDon.ChoDuyet => "Chờ duyệt",
        TrangThaiDon.DaDuyet => "Đã duyệt",
        TrangThaiDon.TuChoi => "Từ chối",
        _ => t.ToString()
    };

    public static string Mau(this TrangThaiDon t) => t switch
    {
        TrangThaiDon.ChoDuyet => "warning text-dark",
        TrangThaiDon.DaDuyet => "success",
        TrangThaiDon.TuChoi => "danger",
        _ => "secondary"
    };
}

public class DonNghi
{
    public int Id { get; set; }
    public int NguoiDungId { get; set; }
    public NguoiDung NguoiDung { get; set; }

    public LoaiNghi Loai { get; set; }

    [Column(TypeName = "date")]
    public DateTime TuNgay { get; set; }
    [Column(TypeName = "date")]
    public DateTime DenNgay { get; set; }
    public int SoNgay { get; set; }

    public string LyDo { get; set; }
    public string TepDinhKem { get; set; }   // tên file lưu trên đĩa (App_Data/don)
    public string TenTepGoc { get; set; }

    public TrangThaiDon TrangThai { get; set; } = TrangThaiDon.ChoDuyet;
    public DateTime NgayTao { get; set; } = DateTime.Now;

    public int? NguoiDuyetId { get; set; }
    public NguoiDung NguoiDuyet { get; set; }
    public DateTime? NgayDuyet { get; set; }
    public string LyDoTuChoi { get; set; }
}

public class TaiLieu
{
    public int Id { get; set; }
    public string TieuDe { get; set; }
    public string TenFile { get; set; }
    public DateTime NgayTai { get; set; } = DateTime.Now;
    public List<TaiLieuDoan> Doans { get; set; } = new();
}

public class TaiLieuDoan
{
    public int Id { get; set; }
    public int TaiLieuId { get; set; }
    public TaiLieu TaiLieu { get; set; }
    public int ThuTu { get; set; }
    public string NoiDung { get; set; }
    public string EmbeddingJson { get; set; }   // vector embedding dạng JSON, null nếu chưa tạo
}

public class ChatLog
{
    public int Id { get; set; }
    public int NguoiDungId { get; set; }
    public NguoiDung NguoiDung { get; set; }
    public DateTime ThoiGian { get; set; } = DateTime.Now;

    public string CauHoi { get; set; }
    public string CauHoiVietLai { get; set; }
    public string Intent { get; set; }
    public string NguonPhanTich { get; set; }   // LLM hoặc QUY_TAC
    public string Nhanh { get; set; }           // RAG, DB, RAG+DB, MAU, TU_CHOI
    public string ThucThe { get; set; }
    public string NguonTaiLieu { get; set; }
    public string DuLieuDb { get; set; }
    public string CauTraLoi { get; set; }
    public int ThoiGianMs { get; set; }
    public string Loi { get; set; }
}
