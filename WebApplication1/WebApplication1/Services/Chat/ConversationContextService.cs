using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Services.Chat
{
    /// <summary>
    /// Реализация сервиса управления контекстом диалога и формирования запросов к LLM.
    /// </summary>
    public class ConversationContextService : IConversationContextService
    {
        private static readonly Dictionary<string, (string Code, string Name)> SupportedLanguages =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = ("en", "English"),
                ["english"] = ("en", "English"),
                ["ru"] = ("ru", "Русский"),
                ["russian"] = ("ru", "Русский"),
                ["pl"] = ("pl", "Polski"),
                ["polish"] = ("pl", "Polski")
            };

        private static readonly HashSet<string> SupportedScenarios =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "cafe", "airport", "hotel", "job_interview", "doctor", "free_talk", "restaurant", "store"
            };

        private static readonly HashSet<string> SupportedLevels =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "A1", "A2", "A1-A2", "B1", "B2", "C1", "C2"
            };

        public ContextValidationResult ValidateContextParameters(ChatRequest request, DialogSession? session)
        {
            // 1. Валидация языка
            var rawLang = !string.IsNullOrWhiteSpace(request.Lang) ? request.Lang : (session?.Lang ?? "en");
            if (!SupportedLanguages.TryGetValue(rawLang.Trim(), out var langInfo))
            {
                return new ContextValidationResult(
                    IsValid: false,
                    ErrorMessage: $"Неподдерживаемый язык '{rawLang}'. Поддерживаются только: en, ru, pl.");
            }

            // 2. Валидация сценария
            var rawScenario = !string.IsNullOrWhiteSpace(request.Scenario) ? request.Scenario : (session?.ScenarioCode ?? "cafe");
            var scenario = rawScenario.Trim().ToLowerInvariant();
            if (!SupportedScenarios.Contains(scenario))
            {
                return new ContextValidationResult(
                    IsValid: false,
                    ErrorMessage: $"Недопустимый сценарий. Допустимые сценарии: {string.Join(", ", SupportedScenarios)}.");
            }

            // 3. Валидация уровня
            var rawLevel = !string.IsNullOrWhiteSpace(request.Level) ? request.Level : (session?.Level ?? "A1-A2");
            var level = rawLevel.Trim().ToUpperInvariant();
            if (!SupportedLevels.Contains(level))
            {
                return new ContextValidationResult(
                    IsValid: false,
                    ErrorMessage: $"Недопустимый уровень. Допустимые уровни: {string.Join(", ", SupportedLevels)}.");
            }

            return new ContextValidationResult(
                IsValid: true,
                LanguageCode: langInfo.Code,
                LanguageName: langInfo.Name,
                Scenario: scenario,
                Level: level);
        }

        public string BuildSystemPrompt(string scenario, string languageName, string languageCode, string level)
        {
            return $"Ты языковой тренажер LinguaBot. Сценарий: {scenario}. " +
                   $"Язык общения: {languageName} ({languageCode}). Уровень ученика: {level}. " +
                   $"Отвечай кратко, естественно и поддерживай живой диалог по заданному сценарию. " +
                   $"Если ученик делает грубые языковые ошибки на языке {languageName}, мягко исправь их в скобках.";
        }

        public async Task<object> BuildGeminiPayloadAsync(
            AppDbContext db,
            int? sessionId,
            string userMessage,
            string systemPrompt,
            CancellationToken cancellationToken = default)
        {
            var contents = new List<object>();

            if (sessionId.HasValue)
            {
                var history = await db.DialogMessages
                    .Where(m => m.DialogSessionId == sessionId.Value)
                    .OrderByDescending(m => m.Id)
                    .Take(10)
                    .OrderBy(m => m.Id)
                    .ToListAsync(cancellationToken);

                foreach (var msg in history)
                {
                    var role = msg.Sender.Equals("Bot", StringComparison.OrdinalIgnoreCase) ? "model" : "user";
                    contents.Add(new
                    {
                        role = role,
                        parts = new[] { new { text = msg.ContentText } }
                    });
                }
            }

            // Добавляем текущую реплику ученика
            contents.Add(new
            {
                role = "user",
                parts = new[] { new { text = userMessage } }
            });

            return new
            {
                contents = contents,
                systemInstruction = new
                {
                    parts = new[] { new { text = systemPrompt } }
                }
            };
        }
    }
}
