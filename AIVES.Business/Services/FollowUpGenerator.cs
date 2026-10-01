using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AIVES.Business.Interfaces;
using AIVES.Business.Models;

namespace AIVES.Business.Services;

// "Giám khảo AI": đọc câu trả lời của sinh viên và quyết định có cần hỏi xoáy hay không.
// Có ApiKey thì hỏi DeepSeek. Không có key hoặc gọi lỗi thì dùng bộ luật dự phòng để buổi thi không bao giờ bị kẹt.
public class FollowUpGenerator : IFollowUpGenerator
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private const int MinWords = 12;

    private readonly AiOptions _options;

    public FollowUpGenerator(AiOptions options) => _options = options;

    public async Task<FollowUpDecision> DecideAsync(FollowUpContext context, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            try
            {
                var decision = await AskModelAsync(context, ct);
                if (decision is not null) return decision;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException or KeyNotFoundException)
            {
                // rơi xuống bộ luật dự phòng
            }
        }

        return Heuristic(context);
    }

    private async Task<FollowUpDecision?> AskModelAsync(FollowUpContext c, CancellationToken ct)
    {
        var language = c.Locale == "en-US" ? "English" : "Vietnamese";
        var system =
            "You are a strict but fair university oral-exam examiner. You are given the main question, the key points a good answer " +
            "should cover (may be empty) and the dialogue so far. Decide whether ONE follow-up question is needed. Ask a follow-up only when " +
            "the latest answer is vague, misses key points, or contradicts something said earlier. Do not ask when the answer is adequate. " +
            $"The follow-up must be one short question in {language}. Reply with JSON only: " +
            "{\"ask\": true|false, \"question\": string|null, \"reason\": string}. \"reason\" is one short sentence in " +
            $"{language} explaining why, shown to the lecturer.";

        var user = new StringBuilder()
            .AppendLine($"Main question: {c.Question}")
            .AppendLine($"Key points: {(string.IsNullOrWhiteSpace(c.ExpectedPoints) ? "(none)" : c.ExpectedPoints)}")
            .AppendLine("Dialogue:");
        foreach (var (exQ, exA) in c.Exchanges)
            user.AppendLine($"Examiner: {exQ}").AppendLine($"Student: {(string.IsNullOrWhiteSpace(exA) ? "(no answer)" : exA)}");

        // DeepSeek dùng giao thức tương thích OpenAI
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.deepseek.com/chat/completions");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Content = JsonContent.Create(new
        {
            model = _options.Model,
            max_tokens = 300,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = system },
                new { role = "user", content = user.ToString() }
            }
        });

        using var response = await Http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var text = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end < start) return null;

        using var json = JsonDocument.Parse(text[start..(end + 1)]);
        var root = json.RootElement;
        var ask = root.TryGetProperty("ask", out var a) && a.ValueKind == JsonValueKind.True;
        var question = root.TryGetProperty("question", out var q) && q.ValueKind == JsonValueKind.String ? q.GetString() : null;
        var reason = root.TryGetProperty("reason", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() : null;

        return new FollowUpDecision(ask && !string.IsNullOrWhiteSpace(question), question?.Trim(), reason?.Trim());
    }

    // ponytail: luật đơn giản (câu trả lời quá ngắn, hoặc thiếu ý chính). Không phát hiện được mâu thuẫn, cần LLM cho phần đó.
    internal static FollowUpDecision Heuristic(FollowUpContext c)
    {
        var vi = c.Locale != "en-US";
        var answer = c.Exchanges[^1].Answer ?? "";

        if (answer.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length < MinWords)
            return new FollowUpDecision(true,
                vi ? "Bạn có thể giải thích rõ hơn và cho một ví dụ cụ thể không?"
                   : "Could you explain that in more detail and give a concrete example?",
                vi ? "Câu trả lời quá ngắn hoặc mơ hồ." : "The answer is too short or vague.");

        var fullAnswer = Normalize(string.Join(' ', c.Exchanges.Select(e => e.Answer)));
        var asked = Normalize(string.Join(' ', c.Exchanges.Select(e => e.Question)));
        var points = (c.ExpectedPoints ?? "").Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Ý chính chưa được nhắc tới và chưa từng bị hỏi lại
        var missing = points.FirstOrDefault(p => !fullAnswer.Contains(Normalize(p)) && !asked.Contains(Normalize(p)));
        if (missing is null) return new FollowUpDecision(false, null, null);

        return new FollowUpDecision(true,
            vi ? $"Bạn chưa nhắc đến \"{missing}\". Bạn có thể làm rõ ý này không?"
               : $"You have not mentioned \"{missing}\". Could you clarify that?",
            vi ? $"Câu trả lời thiếu ý: {missing}." : $"The answer misses the point: {missing}.");
    }

    // Bỏ dấu + chữ thường để so khớp văn bản STT (có thể sai dấu) với từ khóa
    private static string Normalize(string s)
    {
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var ch in d)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return sb.ToString().Replace('đ', 'd').Replace('Đ', 'D').ToLowerInvariant();
    }
}
