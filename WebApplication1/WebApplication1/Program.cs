using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

// ===== ТОЛЬКО ЭТО НУЖНО МЕНЯТЬ =====
var apiKey = "API_KEY";
var model = "gemini-3.6-flash";
var showErrors = true; // пока тестируете: true. На боевом сайте поставьте false
// ===================================

var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";

app.MapPost("/api/chat", async (ChatRequest request, IHttpClientFactory factory) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
        return Results.BadRequest(new { text = "Сообщение не может быть пустым." });

    if (request.Message.Length > 2000)
        return Results.BadRequest(new { text = "Сообщение слишком длинное." });

    try
    {
        var body = new
        {
            contents = new[] { new { parts = new[] { new { text = request.Message } } } },
            systemInstruction = new
            {
                parts = new[] { new { text = "Ты дружелюбный ассистент на сайте swinki.ru. Отвечай кратко, емко и на русском языке." } }
            }
        };

        var http = factory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body)
        };
        req.Headers.Add("x-goog-api-key", apiKey);

        using var response = await http.SendAsync(req);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Gemini error {(int)response.StatusCode}: {err}");

            var msg = showErrors
                ? $"Ошибка Google {(int)response.StatusCode}: {err}"
                : "Сервис ИИ временно недоступен.";
            return Results.Json(new { text = msg }, statusCode: 502);
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        string? aiText = null;
        if (json.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0 &&
            candidates[0].TryGetProperty("content", out var content) &&
            content.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
        {
            aiText = string.Concat(parts.EnumerateArray()
                .Select(p => p.TryGetProperty("text", out var t) ? t.GetString() : null));
        }

        return string.IsNullOrEmpty(aiText)
            ? Results.Json(new { text = "Не удалось получить ответ. Попробуйте переформулировать вопрос." }, statusCode: 502)
            : Results.Ok(new { text = aiText });
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex);
        var msg = showErrors ? $"Ошибка сервера: {ex.Message}" : "Внутренняя ошибка сервера.";
        return Results.Json(new { text = msg }, statusCode: 500);
    }
});

app.Run();

public record ChatRequest(string Message);