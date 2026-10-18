using System.ComponentModel.DataAnnotations;
using ChamCongAI.Models;

namespace ChamCongAI.ViewModels;

public class LoginVm
{
    [Required(ErrorMessage = "Nhập tên đăng nhập")]
    [Display(Name = "Tên đăng nhập")]
    public string TenDangNhap { get; set; }

    [Required(ErrorMessage = "Nhập mật khẩu")]
    [Display(Name = "Mật khẩu")]
    public string MatKhau { get; set; }
}

public class HoiRequest
{
    public string CauHoi { get; set; }
    public List<ChamCongAI.Services.ChatTurn> LichSu { get; set; }
}

public class DonNghiVm
{
    [Display(Name = "Loại nghỉ")]
    public LoaiNghi Loai { get; set; }

    [Required(ErrorMessage = "Chọn ngày bắt đầu")]
    [Display(Name = "Từ ngày")]
    [DataType(DataType.Date)]
    public DateTime TuNgay { get; set; } = DateTime.Today.AddDays(3);

    [Required(ErrorMessage = "Chọn ngày kết thúc")]
    [Display(Name = "Đến ngày")]
    [DataType(DataType.Date)]
    public DateTime DenNgay { get; set; } = DateTime.Today.AddDays(3);

    [Required(ErrorMessage = "Nhập lý do")]
    [Display(Name = "Lý do")]
    public string LyDo { get; set; }

    [Display(Name = "Giấy tờ đính kèm (nếu có)")]
    public IFormFile TepDinhKem { get; set; }
}

public class ChamCongThangVm
{
    public DateTime Thang { get; set; }
    public ChamCong HomNay { get; set; }
    public List<ChamCong> Ds { get; set; } = new();
    public ChamCongAI.Services.ThongKeChamCongDto ThongKe { get; set; }
}

public class TongHopDongVm
{
    public NguoiDung NhanVien { get; set; }
    public ChamCongAI.Services.ThongKeChamCongDto ThongKe { get; set; }
}
