using System.ComponentModel.DataAnnotations;

namespace VoiceService
{
    /// <summary>
    /// Настройки сервиса синтеза речи Piper TTS.
    /// </summary>
    public class TtsOptions
    {
        public const string SectionName = "PiperTTS";

        /// <summary>
        /// Путь к исполняемому файлу piper (например, "piper", "piper.exe" или полный путь "/usr/bin/piper").
        /// </summary>
        public string ExecutablePath { get; set; } = "piper";

        /// <summary>
        /// Базовая директория с моделями Piper (относительная к проекту или абсолютная).
        /// </summary>
        public string ModelsDirectory { get; set; } = "piper_models";

        /// <summary>
        /// Язык по умолчанию, если не указан ("ru", "pl", "en").
        /// </summary>
        public string DefaultLanguage { get; set; } = "ru";

        /// <summary>
        /// Соответствие языковых кодов именам файлов моделей ONNX.
        /// </summary>
        public Dictionary<string, string> VoiceModels { get; set; } = new(StringComparer.OrdinalIgnoreCase)
        {
            { "ru", "ru_RU-irina-medium.onnx" },
            { "pl", "pl_PL-gosia-medium.onnx" },
            { "en", "en_US-lessac-medium.onnx" }
        };

        /// <summary>
        /// Таймаут процесса генерации речи в секундах.
        /// </summary>
        [Range(5, 300)]
        public int TimeoutSeconds { get; set; } = 30;
    }
}
