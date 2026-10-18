using ChamCongAI.Models;
using ChamCongAI.Services;
using Microsoft.AspNetCore.Identity;

namespace ChamCongAI.Data;

public static class DbSeeder
{
    // Tài khoản mẫu, mật khẩu chung: 123456
    public static void Seed(AppDbContext db, string contentRoot)
    {
        if (!db.NguoiDungs.Any()) SeedNguoiDungVaChamCong(db);
        if (!db.TaiLieus.Any()) SeedTaiLieu(db, contentRoot);
    }

    private static void SeedNguoiDungVaChamCong(AppDbContext db)
    {
        var hasher = new PasswordHasher<NguoiDung>();
        NguoiDung Tao(string tk, string ten, string vaiTro, string phong, int namVao, NguoiDung ql = null)
        {
            var u = new NguoiDung
            {
                TenDangNhap = tk, HoTen = ten, VaiTro = vaiTro, PhongBan = phong,
                NgayVaoLam = new DateTime(namVao, 3, 1), QuanLy = ql
            };
            u.MatKhauHash = hasher.HashPassword(u, "123456");
            return u;
        }

        var ql = Tao("ql", "Lê Quốc Cường", VaiTro.QuanLy, "Kỹ thuật", 2018);
        var ns = Tao("ns", "Hoàng Thu Hà", VaiTro.NhanSu, "Nhân sự", 2019);
        var nv1 = Tao("nv1", "Nguyễn Văn An", VaiTro.NhanVien, "Kỹ thuật", 2022, ql);
        var nv2 = Tao("nv2", "Trần Thị Bình", VaiTro.NhanVien, "Kỹ thuật", 2023, ql);
        var nv3 = Tao("nv3", "Phạm Minh Đức", VaiTro.NhanVien, "Kỹ thuật", 2024, ql);
        db.NguoiDungs.AddRange(ql, ns, nv1, nv2, nv3);
        db.SaveChanges();

        // ---- Đơn nghỉ mẫu ----
        var hn = DateTime.Today;
        DateTime NgayLamTu(DateTime d) { while (!LichLam.LaNgayLam(d)) d = d.AddDays(1); return d; }
        DateTime NgayLamSau(DateTime d, int soNgay)
        {
            while (soNgay > 0) { d = d.AddDays(1); if (LichLam.LaNgayLam(d)) soNgay--; }
            return d;
        }
        DonNghi Don(NguoiDung nv, LoaiNghi loai, DateTime tu, int soNgayLam, TrangThaiDon tt, string lyDo)
        {
            var t = NgayLamTu(tu);
            var den = NgayLamSau(t, soNgayLam - 1);
            return new DonNghi
            {
                NguoiDungId = nv.Id, Loai = loai, TuNgay = t, DenNgay = den,
                SoNgay = LichLam.DemNgayLam(t, den), LyDo = lyDo, TrangThai = tt,
                NgayTao = t.AddDays(-4),
                NguoiDuyetId = tt == TrangThaiDon.ChoDuyet ? null : ql.Id,
                NgayDuyet = tt == TrangThaiDon.ChoDuyet ? null : t.AddDays(-3)
            };
        }

        var dons = new List<DonNghi>
        {
            Don(nv1, LoaiNghi.PhepNam, hn.AddDays(-35), 1, TrangThaiDon.DaDuyet, "Việc gia đình"),
            Don(nv1, LoaiNghi.PhepNam, hn.AddDays(-20), 2, TrangThaiDon.DaDuyet, "Đi du lịch"),
            Don(nv1, LoaiNghi.PhepNam, hn.AddDays(10), 1, TrangThaiDon.ChoDuyet, "Khám sức khỏe định kỳ"),
            Don(nv2, LoaiNghi.Om, hn.AddDays(-12), 1, TrangThaiDon.DaDuyet, "Sốt, đau họng"),
            Don(nv2, LoaiNghi.PhepNam, hn.AddDays(-50), 3, TrangThaiDon.DaDuyet, "Về quê"),
            Don(nv3, LoaiNghi.KetHon, hn.AddDays(15), 3, TrangThaiDon.ChoDuyet, "Đám cưới của bản thân"),
        };
        dons[2].LyDoTuChoi = null;
        db.DonNghis.AddRange(dons);
        db.SaveChanges();

        // ---- Dữ liệu chấm công 60 ngày gần nhất (tất định, seed cố định) ----
        var rnd = new Random(42);
        foreach (var nv in new[] { nv1, nv2, nv3, ql })
        {
            var nghi = dons.Where(d => d.NguoiDungId == nv.Id && d.TrangThai == TrangThaiDon.DaDuyet).ToList();
            for (var d = hn.AddDays(-60); d < hn; d = d.AddDays(1))
            {
                if (!LichLam.LaNgayLam(d)) continue;
                if (nghi.Any(x => x.TuNgay <= d && x.DenNgay >= d)) continue;

                var r = rnd.NextDouble();
                if (r < 0.04) continue; // vắng không phép

                int phutVao = r < 0.25 ? rnd.Next(6, 45) : rnd.Next(-20, 4);
                var vao = LichLam.GioVao.Add(TimeSpan.FromMinutes(phutVao));
                TimeSpan ra = rnd.NextDouble() < 0.08
                    ? LichLam.GioRa.Subtract(TimeSpan.FromMinutes(rnd.Next(10, 60)))
                    : LichLam.GioRa.Add(TimeSpan.FromMinutes(rnd.Next(0, 40)));

                var (tre, som) = LichLam.TinhTreSom(vao, ra);
                db.ChamCongs.Add(new ChamCong
                {
                    NguoiDungId = nv.Id, Ngay = d.Date, GioVao = vao, GioRa = ra,
                    DiTrePhut = tre, VeSomPhut = som
                });
            }
        }
        db.SaveChanges();
    }

    // Nạp tài liệu chính sách mẫu (chỉ chia đoạn, chưa gọi API; embedding tạo sau ở trang Tài liệu)
    private static void SeedTaiLieu(AppDbContext db, string contentRoot)
    {
        var thuMuc = Path.Combine(contentRoot, "SampleDocs");
        if (!Directory.Exists(thuMuc)) return;

        foreach (var file in Directory.GetFiles(thuMuc, "*.md").OrderBy(f => f))
        {
            var tl = new TaiLieu { TieuDe = Path.GetFileNameWithoutExtension(file), TenFile = Path.GetFileName(file) };
            int stt = 1;
            foreach (var d in VanBan.ChiaDoan(File.ReadAllText(file)))
                tl.Doans.Add(new TaiLieuDoan { ThuTu = stt++, NoiDung = d });
            db.TaiLieus.Add(tl);
        }
        db.SaveChanges();
    }
}
