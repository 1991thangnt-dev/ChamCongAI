using System.Security.Claims;
using ChamCongAI.Data;
using ChamCongAI.Services;
using ChamCongAI.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChamCongAI.Controllers;

[Authorize]
public class ChatController : Controller
{
    private readonly AppDbContext _db;
    private readonly ChatOrchestrator _bot;
    private readonly LlmClient _llm;

    public ChatController(AppDbContext db, ChatOrchestrator bot, LlmClient llm)
    {
        _db = db; _bot = bot; _llm = llm;
    }

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public IActionResult Index()
    {
        ViewBag.ChatSan = _llm.ChatSanSang;
        ViewBag.EmbSan = _llm.EmbeddingSanSang;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Hoi([FromBody] HoiRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.CauHoi)) return BadRequest();

        var user = await _db.NguoiDungs.FindAsync(UserId);
        if (user == null) return Unauthorized();

        var cauHoi = req.CauHoi.Trim();
        if (cauHoi.Length > 500) cauHoi = cauHoi[..500];

        var lichSu = (req.LichSu ?? new List<ChatTurn>())
            .TakeLast(6)
            .Select(t => new ChatTurn
            {
                VaiTro = t.VaiTro == "assistant" ? "assistant" : "user",
                NoiDung = VanBan.Cat(t.NoiDung, 800)
            }).ToList();

        var kq = await _bot.XuLyAsync(user, cauHoi, lichSu);
        return Json(new
        {
            traLoi = kq.CauTraLoi,
            cacBuoc = kq.Buoc.Select(b => new { ten = b.Ten, chiTiet = b.ChiTiet }),
            nguon = kq.Nguon
        });
    }
}
