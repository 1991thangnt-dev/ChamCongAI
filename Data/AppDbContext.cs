using ChamCongAI.Models;
using Microsoft.EntityFrameworkCore;

namespace ChamCongAI.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<NguoiDung> NguoiDungs { get; set; }
    public DbSet<ChamCong> ChamCongs { get; set; }
    public DbSet<DonNghi> DonNghis { get; set; }
    public DbSet<TaiLieu> TaiLieus { get; set; }
    public DbSet<TaiLieuDoan> TaiLieuDoans { get; set; }
    public DbSet<ChatLog> ChatLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<NguoiDung>().HasIndex(x => x.TenDangNhap).IsUnique();
        mb.Entity<NguoiDung>().HasOne(x => x.QuanLy).WithMany()
            .HasForeignKey(x => x.QuanLyId).OnDelete(DeleteBehavior.Restrict);

        mb.Entity<ChamCong>().HasIndex(x => new { x.NguoiDungId, x.Ngay }).IsUnique();
        mb.Entity<ChamCong>().HasOne(x => x.NguoiDung).WithMany()
            .HasForeignKey(x => x.NguoiDungId).OnDelete(DeleteBehavior.Restrict);

        mb.Entity<DonNghi>().HasOne(x => x.NguoiDung).WithMany()
            .HasForeignKey(x => x.NguoiDungId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<DonNghi>().HasOne(x => x.NguoiDuyet).WithMany()
            .HasForeignKey(x => x.NguoiDuyetId).OnDelete(DeleteBehavior.Restrict);

        mb.Entity<TaiLieuDoan>().HasOne(x => x.TaiLieu).WithMany(t => t.Doans)
            .HasForeignKey(x => x.TaiLieuId).OnDelete(DeleteBehavior.Cascade);

        mb.Entity<ChatLog>().HasOne(x => x.NguoiDung).WithMany()
            .HasForeignKey(x => x.NguoiDungId).OnDelete(DeleteBehavior.Restrict);
    }
}
