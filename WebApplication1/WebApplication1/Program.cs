using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Подключаем HttpClient и Контроллеры
builder.Services.AddHttpClient();
builder.Services.AddControllers();

// 2. Безопасно берем строку подключения к БД (из User Secrets или переменных окружения)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

var app = builder.Build();

// Автопроверка таблиц при старте
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseDefaultFiles();
app.UseStaticFiles();

// Безопасно читаем API-ключ и модель из конфигурации
var apiKey = builder.Configuration["Gemini:ApiKey"];
var model = builder.Configuration["Gemini:Model"] ?? "gemini-2.5-flash";
var showErrors = app.Environment.IsDevelopment();

var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";

// Эндпоинт чата, адаптированный под LinguaBot и сохранение в БД
app.MapPost("/api/chat", async (ChatRequest request, IHttpClientFactory factory, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
        return Results.BadRequest(new { text = "Сообщение не может быть пустым." });

    if (request.Message.Length > 2000)
        return Results.BadRequest(new { text = "Сообщение слишком длинное." });

    if (string.IsNullOrWhiteSpace(apiKey))
        return Results.Json(new { text = "API-ключ Gemini не задан в User Secrets." }, statusCode: 500);

    try
    {
        var lang = request.Lang ?? "EN";
        var level = request.Level ?? "A1-A2";
        var scenario = request.Scenario ?? "cafe";

        var systemPrompt = $"Ты языковой тренажер LinguaBot. Сценарий: {scenario}. " +
                           $"Язык общения: {lang}. Уровень ученика: {level}. " +
                           $"Отвечай кратко, естественно и поддерживай диалог по сценарию.";

        var body = new
        {
            contents = new[] { new { parts = new[] { new { text = request.Message } } } },
            systemInstruction = new
            {
                parts = new[] { new { text = systemPrompt } }
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

        if (string.IsNullOrEmpty(aiText))
            return Results.Json(new { text = "Не удалось получить ответ. Попробуйте переформулировать вопрос." }, statusCode: 502);

        // Если передан ID сессии — сохраняем реплику ученика и ответ бота в PostgreSQL
        if (request.SessionId.HasValue)
        {
            db.DialogMessages.Add(new DialogMessage
            {
                DialogSessionId = request.SessionId.Value,
                Sender = "User",
                ContentText = request.Message
            });

            db.DialogMessages.Add(new DialogMessage
            {
                DialogSessionId = request.SessionId.Value,
                Sender = "Bot",
                ContentText = aiText
            });

            await db.SaveChangesAsync();
        }

        return Results.Ok(new { text = aiText });
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex);
        var msg = showErrors ? $"Ошибка сервера: {ex.Message}" : "Внутренняя ошибка сервера.";
        return Results.Json(new { text = msg }, statusCode: 500);
    }
});

app.MapControllers();
app.Run();

public record ChatRequest(
    string Message,
    int? SessionId = null,
    string? Scenario = "cafe",
    string? Lang = "EN",
    string? Level = "A1-A2"
);