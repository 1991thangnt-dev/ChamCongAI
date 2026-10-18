using ChamCongAI.Models;
using ChamCongAI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChamCongAI.Controllers;

[Authorize(Roles = VaiTro.NhanSu)]
public class DanhGiaController : Controller
{
    private readonly DanhGiaService _svc;
    private readonly LlmClient _llm;

    public DanhGiaController(DanhGiaService svc, LlmClient llm)
    {
        _svc = svc; _llm = llm;
    }

    public IActionResult Index()
    {
        ViewBag.ChatSan = _llm.ChatSanSang;
        ViewBag.EmbSan = _llm.EmbeddingSanSang;
        return View(null as BaoCaoDanhGia);
    }

    [HttpPost]
    public async Task<IActionResult> Chay(bool dungLlm)
    {
        ViewBag.ChatSan = _llm.ChatSanSang;
        ViewBag.EmbSan = _llm.EmbeddingSanSang;
        var bc = await _svc.ChayAsync(dungLlm);
        return View("Index", bc);
    }
}
