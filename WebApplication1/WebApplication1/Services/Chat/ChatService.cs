using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Services.Chat
{
    /// <summary>
    /// Сервис оркестрации диалога LinguaBot и отправки запросов к Gemini.
    /// </summary>
    public class ChatService : IChatService
    {
        private readonly IConversationContextService _contextService;
        private readonly AppDbContext _db;
        private readonly HttpClient _httpClient;
        private readonly GeminiOptions _options;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<ChatService> _logger;

        public ChatService(
            IConversationContextService contextService,
            AppDbContext db,
            HttpClient httpClient,
            IOptions<GeminiOptions> options,
            IWebHostEnvironment env,
            ILogger<ChatService> logger)
        {
            _contextService = contextService ?? throw new ArgumentNullException(nameof(contextService));
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _env = env ?? throw new ArgumentNullException(nameof(env));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ChatExecutionResult> ProcessMessageAsync(
            ChatRequest request,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return new ChatExecutionResult(false, null, 400, "Сообщение не может быть пустым.");
            }

            if (request.Message.Length > 2000)
            {
                return new ChatExecutionResult(false, null, 400, "Сообщение слишком длинное.");
            }

            if (string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                return new ChatExecutionResult(false, null, 500, "API-ключ Gemini не задан в User Secrets.");
            }

            // 1. Проверка сессии при передаче SessionId
            DialogSession? session = null;
            if (request.SessionId.HasValue)
            {
                session = await _db.DialogSessions.FindAsync(new object[] { request.SessionId.Value }, cancellationToken);
                if (session == null)
                {
                    return new ChatExecutionResult(false, null, 404, $"Сессия с ID {request.SessionId.Value} не найдена.");
                }
            }

            // 2. Валидация языка, сценария и уровня по белому списку
            var validation = _contextService.ValidateContextParameters(request, session);
            if (!validation.IsValid)
            {
                return new ChatExecutionResult(false, null, 400, validation.ErrorMessage);
            }

            try
            {
                // 3. Формирование системной инструкции и контекста диалога
                var systemPrompt = _contextService.BuildSystemPrompt(
                    validation.Scenario,
                    validation.LanguageName,
                    validation.LanguageCode,
                    validation.Level);

                var payload = await _contextService.BuildGeminiPayloadAsync(
                    _db,
                    request.SessionId,
                    request.Message,
                    systemPrompt,
                    cancellationToken);

                // 4. Отправка запроса в Gemini API
                using var req = new HttpRequestMessage(HttpMethod.Post, _options.EndpointUrl)
                {
                    Content = JsonContent.Create(payload)
                };
                req.Headers.Add("x-goog-api-key", _options.ApiKey);

                using var response = await _httpClient.SendAsync(req, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Gemini error {StatusCode}: {Error}", (int)response.StatusCode, err);

                    var msg = _env.IsDevelopment()
                        ? $"Ошибка Google {(int)response.StatusCode}: {err}"
                        : "Сервис ИИ временно недоступен.";
                    return new ChatExecutionResult(false, null, 502, msg);
                }

                var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);

                string? aiText = null;
                if (json.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0 &&
                    candidates[0].TryGetProperty("content", out var content) &&
                    content.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                {
                    aiText = string.Concat(parts.EnumerateArray()
                        .Select(p => p.TryGetProperty("text", out var t) ? t.GetString() : null));
                }

                if (string.IsNullOrEmpty(aiText))
                {
                    return new ChatExecutionResult(false, null, 502, "Не удалось получить ответ. Попробуйте переформулировать вопрос.");
                }

                // 5. Сохранение сообщений в PostgreSQL при наличии активной сессии
                if (request.SessionId.HasValue)
                {
                    _db.DialogMessages.Add(new DialogMessage
                    {
                        DialogSessionId = request.SessionId.Value,
                        Sender = "User",
                        ContentText = request.Message
                    });

                    _db.DialogMessages.Add(new DialogMessage
                    {
                        DialogSessionId = request.SessionId.Value,
                        Sender = "Bot",
                        ContentText = aiText
                    });

                    await _db.SaveChangesAsync(cancellationToken);
                }

                return new ChatExecutionResult(true, aiText);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке запроса в ChatService");
                var msg = _env.IsDevelopment() ? $"Ошибка сервера: {ex.Message}" : "Внутренняя ошибка сервера.";
                return new ChatExecutionResult(false, null, 500, msg);
            }
        }
    }
}
