using System.ComponentModel.DataAnnotations;

namespace VoiceService
{
    /// <summary>
    /// Настройки сервиса распознавания речи Groq STT.
    /// </summary>
    public class SttOptions
    {
        public const string SectionName = "Groq";

        [Required(AllowEmptyStrings = false, ErrorMessage = "Не указан обязательный параметр 'Groq:ApiKey' в конфигурации или User Secrets.")]
        public string ApiKey { get; set; } = string.Empty;

        public string ModelSTT { get; set; } = "whisper-large-v3";

        public string Endpoint { get; set; } = "https://api.groq.com/openai/v1/audio/transcriptions";

        public string DefaultLanguage { get; set; } = "ru";

        /// <summary>
        /// Максимальный допустимый размер аудиофайла в байтах (по умолчанию 25 МБ для Groq Whisper).
        /// </summary>
        public int MaxFileSizeBytes { get; set; } = 25 * 1024 * 1024;

        /// <summary>
        /// Таймаут HTTP-клиента в секундах.
        /// Примечание: таймауты запросов и повторных попыток контролируются через AddStandardResilienceHandler в Program.cs.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 60;
    }
}
