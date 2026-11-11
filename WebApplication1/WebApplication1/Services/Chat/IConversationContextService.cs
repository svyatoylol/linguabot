using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Services.Chat
{
    public record ContextValidationResult(
        bool IsValid,
        string? ErrorMessage = null,
        string LanguageCode = "en",
        string LanguageName = "English",
        string Scenario = "cafe",
        string Level = "A1-A2"
    );

    /// <summary>
    /// Сервис управления контекстом диалога, ролями и системными промптами.
    /// </summary>
    public interface IConversationContextService
    {
        /// <summary>
        /// Валидирует язык, сценарий и уровень по белому списку (Whitelist).
        /// </summary>
        ContextValidationResult ValidateContextParameters(
            ChatRequest request,
            DialogSession? session);

        /// <summary>
        /// Формирует системную инструкцию (роль персонажа, правила исправления ошибок).
        /// </summary>
        string BuildSystemPrompt(
            string scenario,
            string languageName,
            string languageCode,
            string level);

        /// <summary>
        /// Загружает историю последних сообщений из БД и формирует тело запроса к Gemini (contents + systemInstruction).
        /// </summary>
        Task<object> BuildGeminiPayloadAsync(
            AppDbContext db,
            int? sessionId,
            string userMessage,
            string systemPrompt,
            CancellationToken cancellationToken = default);
    }
}
