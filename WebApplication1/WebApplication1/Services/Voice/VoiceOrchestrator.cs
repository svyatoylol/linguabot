using Microsoft.Extensions.Logging;
using WebApplication1.Models;
using WebApplication1.Services.Chat;

namespace VoiceService
{
    /// <summary>
    /// Результат сквозной голосовой обработки диалога (STT -> Gemini Chat -> TTS).
    /// </summary>
    public record VoiceOrchestrationResult(
        bool IsSuccess,
        string? UserText = null,
        string? AiText = null,
        byte[]? AudioBytes = null,
        string? AudioBase64 = null,
        string? AudioMimeType = "audio/wav",
        string? DetectedLanguage = null,
        double? PronunciationScore = null,
        int StatusCode = 200,
        string? Error = null
    );

    /// <summary>
    /// Интерфейс голосового оркестратора (VoiceOrchestrator), объединяющего
    /// STT (распознавание речи), диалог с LLM (Gemini) и TTS (синтез речи).
    /// </summary>
    public interface IVoiceOrchestrator
    {
        Task<VoiceOrchestrationResult> ProcessVoiceAsync(
            Stream audioStream,
            string fileName = "speech.webm",
            int? sessionId = null,
            string? scenario = null,
            string? language = null,
            string? level = null,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Голосовой оркестратор: принимает голосовой ввод пользователя,
    /// транскрибирует через STT (Whisper), передает в чат с ИИ (Gemini)
    /// и озвучивает ответ через TTS (Piper).
    /// </summary>
    public class VoiceOrchestrator : IVoiceOrchestrator
    {
        private readonly ISttService _sttService;
        private readonly IChatService _chatService;
        private readonly ITtsService _ttsService;
        private readonly ILogger<VoiceOrchestrator> _logger;

        public VoiceOrchestrator(
            ISttService sttService,
            IChatService chatService,
            ITtsService ttsService,
            ILogger<VoiceOrchestrator> logger)
        {
            _sttService = sttService ?? throw new ArgumentNullException(nameof(sttService));
            _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
            _ttsService = ttsService ?? throw new ArgumentNullException(nameof(ttsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<VoiceOrchestrationResult> ProcessVoiceAsync(
            Stream audioStream,
            string fileName = "speech.webm",
            int? sessionId = null,
            string? scenario = null,
            string? language = null,
            string? level = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(audioStream);

            // 1. Шаг STT: распознавание входящей речи пользователя
            _logger.LogInformation("VoiceOrchestrator: Старт распознавания STT для файла {FileName}", fileName);
            GroqTranscriptionReport? transcription;
            try
            {
                transcription = await _sttService.TranscribeStreamAsync(
                    audioStream,
                    language,
                    fileName,
                    cancellationToken: cancellationToken);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Ошибка аргументов STT: {Message}", ex.Message);
                return new VoiceOrchestrationResult(false, null, null, StatusCode: 400, Error: ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Сбой сервиса STT при обработке голоса");
                return new VoiceOrchestrationResult(false, null, null, StatusCode: 500, Error: "Ошибка при распознавании речи.");
            }

            if (transcription == null || string.IsNullOrWhiteSpace(transcription.Text))
            {
                return new VoiceOrchestrationResult(false, null, null, StatusCode: 400, Error: "Речь не распознана.");
            }

            var userText = transcription.Text.Trim();
            var detectedLang = !string.IsNullOrWhiteSpace(transcription.Language)
                ? transcription.Language
                : language;

            // Оценка разборчивости/уверенности произношения на основе метрик Whisper
            double? pronunciationScore = null;
            if (transcription.Segments is { Count: > 0 })
            {
                var validSegments = transcription.Segments.Where(s => s.AvgLogprob.HasValue).ToList();
                if (validSegments.Count > 0)
                {
                    var avgLogprob = validSegments.Average(s => s.AvgLogprob!.Value);
                    pronunciationScore = Math.Round(Math.Clamp(Math.Exp(avgLogprob) * 100.0, 0.0, 100.0), 1);
                }
            }

            _logger.LogInformation("VoiceOrchestrator: Пользователь сказал: '{UserText}' (язык: {Lang}, точность: {Score}%)",
                userText, detectedLang, pronunciationScore?.ToString("F1") ?? "н/д");

            // 2. Шаг Chat: передача текста в сервис диалога с ИИ (Gemini)
            var chatRequest = new ChatRequest(
                Message: userText,
                SessionId: sessionId,
                Scenario: scenario,
                Lang: detectedLang,
                Level: level
            );

            _logger.LogInformation("VoiceOrchestrator: Передача сообщения в ChatService...");
            ChatExecutionResult chatResult;
            try
            {
                chatResult = await _chatService.ProcessMessageAsync(chatRequest, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Сбой ChatService при обработке сообщения");
                return new VoiceOrchestrationResult(
                    false,
                    UserText: userText,
                    StatusCode: 500,
                    Error: "Ошибка при формировании ответа ИИ."
                );
            }

            if (!chatResult.IsSuccess || string.IsNullOrWhiteSpace(chatResult.Text))
            {
                return new VoiceOrchestrationResult(
                    false,
                    UserText: userText,
                    StatusCode: chatResult.StatusCode,
                    Error: chatResult.Error ?? "ИИ не сформировал ответ."
                );
            }

            var aiText = chatResult.Text.Trim();
            _logger.LogInformation("VoiceOrchestrator: Ответ ИИ: '{AiText}'", aiText);

            // 3. Шаг TTS: синтез речи ответа бота (Piper)
            byte[]? audioBytes = null;
            string? audioBase64 = null;
            try
            {
                _logger.LogInformation("VoiceOrchestrator: Синтез речи ответа ИИ через TTS...");
                audioBytes = await _ttsService.SynthesizeBytesAsync(aiText, detectedLang, cancellationToken);
                if (audioBytes is { Length: > 0 })
                {
                    audioBase64 = Convert.ToBase64String(audioBytes);
                }
            }
            catch (Exception ex)
            {
                // Если TTS локально недоступен (например, нет бинарника piper или onnx модели),
                // не ломаем весь диалог — возвращаем текст пользователю, логируя предупреждение
                _logger.LogWarning(ex, "VoiceOrchestrator: Синтез TTS не удался ({Message}), возвращается только текст.", ex.Message);
            }

            return new VoiceOrchestrationResult(
                IsSuccess: true,
                UserText: userText,
                AiText: aiText,
                AudioBytes: audioBytes,
                AudioBase64: audioBase64,
                AudioMimeType: "audio/wav",
                DetectedLanguage: detectedLang,
                PronunciationScore: pronunciationScore,
                StatusCode: 200
            );
        }
    }
}
