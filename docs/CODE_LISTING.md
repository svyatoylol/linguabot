# Листинг программного кода ключевых модулей «LinguaBot»

## 1. Точка входа и маршрутизация (Program.cs)

```csharp
using LinguaBot.Data;
using LinguaBot.Endpoints;
using LinguaBot.Services;
using Microsoft.EntityFrameworkCore;
using VoiceService;
using WebApplication1.Services.Chat;

var builder = WebApplication.CreateBuilder(args);

// 1. База данных PostgreSQL (ApplicationDbContext)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    connectionString = "Host=localhost;Port=5432;Database=linguabot_db;Username=postgres;Password=postgre";
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString)
);

// 2. Отказоустойчивое ядро данных и сервисы обучения
builder.Services.AddSingleton<LinguaDataStore>();
builder.Services.AddSingleton<ScenarioCatalog>();
builder.Services.AddSingleton<IntelligentLanguageEngine>();
builder.Services.AddScoped<PlacementTestGeneratorService>();
builder.Services.AddScoped<ExerciseGeneratorService>();
builder.Services.AddScoped<DialogueAnalysisService>();
builder.Services.AddHttpClient<GeminiService>();

// 3. Сервисы голоса (Piper TTS / Whisper STT) и чата
builder.Services.AddHttpClient();
builder.Services.AddControllers();
builder.Services.AddVoiceServices();
builder.Services.AddChatServices(builder.Configuration);

// 4. Настройка CORS для поддержки PWA и fetch credentials
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Безопасное автоприменение миграций БД (с грациозным фоллбэком при оффлайн-сервере PostgreSQL)
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
    Console.WriteLine("[Database] PostgreSQL migrations applied successfully.");
}
catch (Exception ex)
{
    Console.WriteLine($"[Database Warning] PostgreSQL is not reachable: {ex.Message}. Using resilient LinguaDataStore (App_Data/linguabot_store.json) for offline/demo mode.");
}

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

// 5. Подключение модульных эндпоинтов 15 функций MVP
app.MapAuthEndpoints();
app.MapProfileEndpoints();
app.MapOnboardingEndpoints();
app.MapDialogueEndpoints();
app.MapExerciseEndpoints();
app.MapDictionaryEndpoints();
app.MapDailyPlanAndStatsEndpoints();
app.MapPlacementTestEndpoints();
app.MapVoiceEndpoints();
app.MapChatEndpoints();
app.MapControllers();

// Health check
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "healthy",
    product = "LinguaBot",
    version = "2.0-MVP",
    time = DateTime.UtcNow
}));

app.Run();
```

---

## 2. Диалоговый модуль и отправка реплик (DialogueEndpoints.cs)

```csharp
using LinguaBot.Models;
using LinguaBot.Services;

namespace LinguaBot.Endpoints;

public static class DialogueEndpoints
{
    public static void MapDialogueEndpoints(this WebApplication app)
    {
        // Каталог 10 диалоговых сценариев
        app.MapGet("/api/scenarios", (ScenarioCatalog catalog) => Results.Ok(catalog.GetAll()));

        // Создание диалога
        app.MapPost("/api/dialogues", (CreateDialogueSessionRequest req, HttpContext ctx, LinguaDataStore store, ScenarioCatalog catalog) =>
        {
            var user = AuthEndpoints.GetOrCreateCurrentUser(ctx, store);
            var scenario = catalog.GetById(req.ScenarioId ?? "introduction") ?? catalog.GetAll().First();

            string lang = !string.IsNullOrWhiteSpace(req.TargetLanguage) ? req.TargetLanguage.Trim().ToLowerInvariant() : user.Settings.LearningLanguage;
            string level = !string.IsNullOrWhiteSpace(req.Level) ? req.Level.Trim() : user.Settings.Level;
            string mode = !string.IsNullOrWhiteSpace(req.Mode) ? req.Mode.Trim() : "Standard";

            var session = new DialogueSession
            {
                UserId = user.Id,
                Scenario = scenario.Id,
                ScenarioTitle = scenario.Title,
                LanguageCode = lang,
                Level = level,
                Mode = mode,
                Status = "Active",
                StartedAt = DateTime.UtcNow
            };

            string greeting = mode == "NoCheatSheet"
                ? "Давайте проверим ваши навыки без заготовок! Попробуйте ответить на мой вопрос своими словами, объяснив причину своими формулировками."
                : OnboardingEndpointsExtension.MapOnboardingEndpointsExtensionGetWelcome(scenario.Id, lang);

            var welcomeMessage = new DialogueMessage
            {
                SessionId = session.Id,
                Role = "assistant",
                Content = greeting
            };

            session.Messages.Add(welcomeMessage);
            store.Sessions[session.Id] = session;
            store.Messages[welcomeMessage.Id] = welcomeMessage;
            user.Sessions.Add(session);
            store.SaveToDisk();

            return Results.Ok(new
            {
                dialogueId = session.Id,
                sessionId = session.Id,
                mode = session.Mode,
                scenario = new { scenario.Id, scenario.Title, scenario.Description, scenario.BotRole, scenario.UserGoal },
                welcomeMessage = welcomeMessage.Content
            });
        });

        // Отправка сообщения и ответ бота (время отклика ≤ 3 сек)
        app.MapPost("/api/dialogues/{sessionId:guid}/messages", async (
            Guid sessionId,
            SendMessageRequest req,
            HttpContext ctx,
            LinguaDataStore store,
            ScenarioCatalog catalog,
            GeminiService gemini,
            IntelligentLanguageEngine engine,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Content))
                return Results.BadRequest(new { error = "Сообщение не может быть пустым." });

            if (!store.Sessions.TryGetValue(sessionId, out var session))
                return Results.NotFound(new { error = "Диалог не найден." });

            if (session.Status != "Active")
                return Results.BadRequest(new { error = "Этот диалог уже завершён." });

            bool usedHint = req.UsedHint ?? false;
            if (usedHint) session.UsedHintsCount++;
            else session.IndependentTurnsCount++;

            var userMsg = new DialogueMessage
            {
                SessionId = session.Id,
                Role = "user",
                Content = req.Content.Trim(),
                UsedHint = usedHint,
                IsVoice = req.IsVoice ?? false,
                ReplyToMessageId = req.ReplyToMessageId
            };

            store.Messages[userMsg.Id] = userMsg;
            session.Messages.Add(userMsg);

            var history = store.Messages.Values
                .Where(m => m.SessionId == sessionId)
                .OrderBy(m => m.CreatedAt)
                .Take(20)
                .ToList();

            var scenario = catalog.GetById(session.Scenario);

            string botReply;
            try
            {
                var prompt = new System.Text.StringBuilder();
                prompt.AppendLine("You are LinguaBot, a language learning tutor.");
                prompt.AppendLine($"Dialogue scenario: {session.Scenario}");
                prompt.AppendLine($"Learning language: {session.LanguageCode}");
                prompt.AppendLine($"User level: {session.Level}");
                if (session.Mode == "NoCheatSheet") prompt.AppendLine("Mode: NoCheatSheet (test user's spontaneous formulation)");

                foreach (var h in history)
                {
                    prompt.AppendLine($"{(h.Role == "user" ? "User" : "Assistant")}: {h.Content}");
                }

                botReply = await gemini.GenerateResponseAsync(prompt.ToString(), ct);
            }
            catch
            {
                botReply = engine.GenerateDialogueReply(session.Scenario, session.LanguageCode, session.Level, userMsg.Content, history.Count, session.Mode == "NoCheatSheet");
            }

            var assistantMsg = new DialogueMessage
            {
                SessionId = session.Id,
                Role = "assistant",
                Content = botReply
            };

            store.Messages[assistantMsg.Id] = assistantMsg;
            session.Messages.Add(assistantMsg);
            store.SaveToDisk();

            return Results.Ok(new
            {
                userMessage = new { userMsg.Id, userMsg.Role, userMsg.Content, userMsg.UsedHint, userMsg.IsVoice, userMsg.CreatedAt },
                assistantMessage = new { assistantMsg.Id, assistantMsg.Role, assistantMsg.Content, assistantMsg.CreatedAt }
            });
        });

        // Завершение диалога и запуск анализа
        app.MapPost("/api/dialogues/{sessionId:guid}/finish", async (Guid sessionId, LinguaDataStore store, DialogueAnalysisService analysisService, CancellationToken ct) =>
        {
            if (!store.Sessions.TryGetValue(sessionId, out var session))
                return Results.NotFound(new { error = "Диалог не найден." });

            session.Status = "Completed";
            session.FinishedAt = DateTime.UtcNow;

            var analysis = await analysisService.AnalyzeAsync(sessionId, ct);
            store.SaveToDisk();

            return Results.Ok(new { message = "Диалог успешно завершён.", analysisId = analysis.Id, status = session.Status });
        });
    }
}
```

---

## 3. Интеллектуальный языковой движок и анализ ошибок (IntelligentLanguageEngine.cs)

```csharp
using System.Text.RegularExpressions;
using LinguaBot.Models;
using LinguaBot.Models.Analysis;
using LinguaBot.Models.Exercises;

namespace LinguaBot.Services;

public class IntelligentLanguageEngine
{
    private readonly ScenarioCatalog _catalog;

    public IntelligentLanguageEngine(ScenarioCatalog catalog)
    {
        _catalog = catalog;
    }

    public string GenerateDialogueReply(string scenarioId, string languageCode, string level, string userMessage, int turnCount, bool isNoCheatSheet = false)
    {
        var lang = (languageCode ?? "en").ToLowerInvariant();
        if (lang == "es") return GenerateSpanishReply(scenarioId, level, userMessage, turnCount, isNoCheatSheet);
        if (lang == "ru") return GenerateRussianReply(scenarioId, level, userMessage, turnCount, isNoCheatSheet);
        return GenerateEnglishReply(scenarioId, level, userMessage, turnCount, isNoCheatSheet);
    }

    private string GenerateEnglishReply(string scenarioId, string level, string userText, int turn, bool isNoCheatSheet)
    {
        var lower = (userText ?? "").ToLowerInvariant();

        if (isNoCheatSheet)
        {
            if (lower.Contains("because") || lower.Contains("due to") || lower.Contains("delay"))
                return "That is a very clear and professional explanation! Thank you for clarifying. How will this affect our timeline for next week?";
            return "Good attempt! Could you try expressing that again using your own words, explaining the reason for the delay or your main preference?";
        }

        switch (scenarioId)
        {
            case "cafe":
                if (lower.Contains("latte") || lower.Contains("coffee") || lower.Contains("cappuccino"))
                    return "Excellent choice! What size would you prefer — small, medium, or large? And would you like whole milk, oat milk, or almond milk?";
                if (lower.Contains("large") || lower.Contains("medium") || lower.Contains("small"))
                    return "Got it! That will be $4.50. Would you like to pay by card or in cash today?";
                return "Hello and welcome to Central Perk! What can I get started for you today?";

            case "introduction":
                if (turn == 1 || lower.Contains("my name") || lower.Contains("i am") || lower.Contains("i'm"))
                    return "Nice to meet you! Where are you from originally, and what do you enjoy doing in your free time?";
                return "Hi there! Welcome to our language practice session. Could you tell me a little bit about yourself?";

            default:
                return "That's very interesting! Could you tell me more about your thoughts on this, and what you think we should focus on next?";
        }
    }

    public DialogueAnalysisResult AnalyzeDialogue(string scenarioId, string languageCode, string level, List<(string Role, string Content)> messages)
    {
        var userMessages = messages.Where(m => m.Role == "user").Select(m => m.Content).ToList();
        var detected = new List<DetectedErrorResult>();
        var strengths = new List<string>();

        foreach (var msg in userMessages)
        {
            var text = msg.Trim();

            if (Regex.IsMatch(text, @"\bI\s+am\s+(work|live|want|like)\b", RegexOptions.IgnoreCase))
            {
                var match = Regex.Match(text, @"\bI\s+am\s+(work|live|want|like)\b", RegexOptions.IgnoreCase);
                detected.Add(new DetectedErrorResult
                {
                    OriginalText = match.Value,
                    CorrectedText = $"I {match.Groups[1].Value}",
                    Category = "Grammar",
                    Severity = 2,
                    Explanation = "В Present Simple с обычными смысловыми глаголами вспомогательный глагол 'am' не используется. Правильно: 'I work', а не 'I am work'."
                });
            }

            if (Regex.IsMatch(text, @"\b(would like|could you|in my opinion|convenient)\b", RegexOptions.IgnoreCase))
            {
                strengths.Add("Отличное использование вежливых клише и естественных идиом ('would like', 'could you', 'in my opinion').");
            }
        }

        var upToThreeErrors = detected.Take(3).ToList();
        var strengthSummary = strengths.Distinct().Take(2).ToList();
        if (strengthSummary.Count == 0) strengthSummary.Add("Хорошая беглость речи и готовность вести диалог без пауз.");

        return new DialogueAnalysisResult
        {
            OverallFeedback = upToThreeErrors.Count == 0
                ? "Превосходная беседа! Вы свободно поддержали разговор, правильно поняли собеседника и использовали подходящие формулировки."
                : $"Хорошая практика! Обратите внимание на {upToThreeErrors.Count} разобранных правила ниже для перехода к абсолютно свободной речи.",
            Strengths = string.Join(" ", strengthSummary),
            RecommendedLevel = level ?? "B1-B2",
            Errors = upToThreeErrors
        };
    }
}
```
