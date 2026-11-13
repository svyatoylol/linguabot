namespace VoiceService
{
    /// <summary>
    /// Интерфейс сервиса синтеза речи (Text-to-Speech).
    /// </summary>
    public interface ITtsService
    {
        /// <summary>
        /// Синтезирует речь из текста и возвращает поток с аудиоданными (WAV).
        /// </summary>
        /// <param name="text">Текст для озвучивания.</param>
        /// <param name="language">Код языка (например, "ru", "pl", "en").</param>
        /// <param name="cancellationToken">Токен отмены.</param>
        Task<Stream> SynthesizeStreamAsync(string text, string? language = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Синтезирует речь из текста и возвращает массив байтов (WAV).
        /// </summary>
        /// <param name="text">Текст для озвучивания.</param>
        /// <param name="language">Код языка (например, "ru", "pl", "en").</param>
        /// <param name="cancellationToken">Токен отмены.</param>
        Task<byte[]> SynthesizeBytesAsync(string text, string? language = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Синтезирует речь из текста напрямую в указанный файл на диске.
        /// </summary>
        /// <param name="text">Текст для озвучивания.</param>
        /// <param name="outputFilePath">Полный путь к сохраняемому аудиофайлу (.wav).</param>
        /// <param name="language">Код языка (например, "ru", "pl", "en").</param>
        /// <param name="cancellationToken">Токен отмены.</param>
        Task SynthesizeToFileAsync(string text, string outputFilePath, string? language = null, CancellationToken cancellationToken = default);
    }
}
