using ChamCongAI.Data;
using ChamCongAI.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(o =>
    o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.AccessDeniedPath = "/Account/AccessDenied";
    });

builder.Services.AddHttpClient();
builder.Services.AddSingleton(builder.Configuration.GetSection("Ai").Get<AiOptions>() ?? new AiOptions());

builder.Services.AddScoped<LlmClient>();
builder.Services.AddScoped<RetrievalService>();
builder.Services.AddScoped<TaiLieuService>();
builder.Services.AddScoped<ChatTools>();
builder.Services.AddScoped<QuestionAnalyzer>();
builder.Services.AddScoped<ChatOrchestrator>();
builder.Services.AddScoped<DanhGiaService>();

var app = builder.Build();

// Tự tạo database + dữ liệu mẫu ở lần chạy đầu tiên
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    DbSeeder.Seed(db, app.Environment.ContentRootPath);
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Chat}/{action=Index}/{id?}");

app.Run();