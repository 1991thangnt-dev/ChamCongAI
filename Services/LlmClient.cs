using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ChamCongAI.Services;

public class EndpointOptions
{
    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; }
    public string ApiKey { get; set; }
    public string Model { get; set; }

    public bool SanSang =>
        Enabled && !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(Model)
        && !string.IsNullOrWhiteSpace(ApiKey) && !ApiKey.StartsWith("DAN_API_KEY");
}

public class AiOptions
{
    public EndpointOptions Chat { get; set; } = new();
    public EndpointOptions Embedding { get; set; } = new();
}

public record ChatMsg(string Role, string Content);

/// <summary>
/// Lớp duy nhất gọi mô hình AI, dùng chuẩn "OpenAI-compatible" nên đổi nhà cung cấp
/// (Groq, OpenRouter, Gemini, Jina, Ollama...) chỉ cần sửa BaseUrl / ApiKey / Model trong appsettings.json.
/// </summary>
public class LlmClient
{
    private readonly IHttpClientFactory _factory;
    private readonly AiOptions _opt;

    public LlmClient(IHttpClientFactory factory, AiOptions opt)
    {
        _factory = factory;
        _opt = opt;
    }

    public bool ChatSanSang => _opt.Chat.SanSang;
    public bool EmbeddingSanSang => _opt.Embedding.SanSang;
    public string TenModelChat => _opt.Chat.Model;
    public string TenModelEmbedding => _opt.Embedding.Model;

    public async Task<string> ChatAsync(IEnumerable<ChatMsg> messages, double temperature = 0.2, CancellationToken ct = default)
    {
        if (!ChatSanSang)
            throw new InvalidOperationException("Chưa cấu hình API key cho mô hình chat (Ai:Chat:ApiKey trong appsettings.json).");

        var body = new
        {
            model = _opt.Chat.Model,
            temperature,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToList()
        };
        using var doc = await PostAsync(_opt.Chat, "chat/completions", body, ct);
        var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        return content ?? "";
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        if (!EmbeddingSanSang)
            throw new InvalidOperationException("Chưa cấu hình API key cho embedding (Ai:Embedding:ApiKey trong appsettings.json).");

        if (text.Length > 6000) text = text[..6000];
        var body = new { model = _opt.Embedding.Model, input = text };
        using var doc = await PostAsync(_opt.Embedding, "embeddings", body, ct);
        var arr = doc.RootElement.GetProperty("data")[0].GetProperty("embedding");
        var v = new float[arr.GetArrayLength()];
        int i = 0;
        foreach (var e in arr.EnumerateArray()) v[i++] = e.GetSingle();
        return v;
    }

    private async Task<JsonDocument> PostAsync(EndpointOptions ep, string path, object body, CancellationToken ct)
    {
        var http = _factory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(60);
        var url = ep.BaseUrl.TrimEnd('/') + "/" + path;
        var json = JsonSerializer.Serialize(body);

        for (int lan = 1; ; lan++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ep.ApiKey);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var resp = await http.SendAsync(req, ct);
            var text = await resp.Content.ReadAsStringAsync(ct);
            if (resp.IsSuccessStatusCode) return JsonDocument.Parse(text);

            int code = (int)resp.StatusCode;
            // Quá giới hạn (429) hoặc lỗi máy chủ: chờ rồi thử lại tối đa 3 lần
            if ((code == 429 || code >= 500) && lan < 3)
            {
                await Task.Delay(1500 * lan, ct);
                continue;
            }
            throw new HttpRequestException($"API trả về {code}: {VanBan.Cat(text, 300)}");
        }
    }
}
