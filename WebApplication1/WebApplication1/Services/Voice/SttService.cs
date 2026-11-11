using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace VoiceService
{
    /// <summary>
    /// Интерфейс сервиса распознавания речи (Speech-to-Text).
    /// </summary>
    public interface ISttService
    {
        Task<GroqTranscriptionReport?> TranscribeStreamAsync(
            Stream audioStream,
            string? language = null,
            string fileName = "speech.wav",
            string? prompt = null,
            CancellationToken cancellationToken = default);

        Task<GroqTranscriptionReport?> TranscribeBytesAsync(
            byte[] audioBytes,
            string? language = null,
            string fileName = "speech.wav",
            string? prompt = null,
            CancellationToken cancellationToken = default);

        Task<GroqTranscriptionReport?> TranscribeFileAsync(
            string filePath,
            string? language = null,
            string? prompt = null,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Сервис распознавания речи (Speech-to-Text) через API Groq (Whisper).
    /// Принимает аудиопоток и возвращает отчет транскрипции для оркестратора.
    /// </summary>
    public class SttService : ISttService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<SttService> _logger;
        private readonly SttOptions _options;

        public SttService(
            HttpClient httpClient,
            IOptions<SttOptions> options,
            ILogger<SttService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// Распознать речь из потока (Stream).
        /// </summary>
        /// <param name="audioStream">Поток с аудиоданными.</param>
        /// <param name="language">Язык речи (ISO-код, например "en", "ru", "pl"). Регистр нормализуется автоматически. Передайте "auto" для автоопределения.</param>
        /// <param name="fileName">Имя файла с расширением для определения формата (wav, mp3, ogg, m4a, webm, flac).</param>
        /// <param name="prompt">Опциональный контекстный текст/подсказка для Whisper.</param>
        /// <param name="cancellationToken">Токен отмены операции.</param>
        /// <remarks>
        /// Внимание: если переданный поток поддерживает позиционирование (<see cref="Stream.CanSeek"/>),
        /// его позиция будет автоматически сброшена в 0 (<c>Position = 0</c>).
        /// </remarks>
        public async Task<GroqTranscriptionReport?> TranscribeStreamAsync(
            Stream audioStream,
            string? language = null,
            string fileName = "speech.wav",
            string? prompt = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(audioStream);

            if (audioStream.CanSeek)
            {
                EnsureSizeWithinLimit(audioStream.Length, fileName);
                if (audioStream.Position > 0)
                {
                    audioStream.Position = 0;
                }
            }

            // Нормализуем язык в нижний регистр для Whisper API (например, "EN" -> "en")
            var rawLang = language ?? _options.DefaultLanguage;
            var targetLanguage = rawLang?.Trim().ToLowerInvariant();

            using var content = new MultipartFormDataContent();

            var fileContent = new StreamContent(audioStream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(GetMimeType(fileName));
            content.Add(fileContent, "file", fileName);

            content.Add(new StringContent(_options.ModelSTT), "model");
            // verbose_json возвращает duration, language и segments с таймкодами
            content.Add(new StringContent("verbose_json"), "response_format");

            if (!string.IsNullOrWhiteSpace(targetLanguage) && !targetLanguage.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                content.Add(new StringContent(targetLanguage), "language");
            }

            if (!string.IsNullOrWhiteSpace(prompt))
            {
                content.Add(new StringContent(prompt), "prompt");
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint)
            {
                Content = content
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Ошибка Groq STT API [{StatusCode}]: {Error}", response.StatusCode, errorBody);
                throw new HttpRequestException($"Groq STT API вернул ошибку {(int)response.StatusCode}: {errorBody}");
            }

            return await response.Content.ReadFromJsonAsync<GroqTranscriptionReport>(cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Распознать речь из массива байт.
        /// </summary>
        public async Task<GroqTranscriptionReport?> TranscribeBytesAsync(
            byte[] audioBytes,
            string? language = null,
            string fileName = "speech.wav",
            string? prompt = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(audioBytes);

            EnsureSizeWithinLimit(audioBytes.Length, fileName);

            using var memoryStream = new MemoryStream(audioBytes);
            return await TranscribeStreamAsync(memoryStream, language, fileName, prompt, cancellationToken);
        }

        /// <summary>
        /// Распознать речь из локального аудиофайла.
        /// </summary>
        public async Task<GroqTranscriptionReport?> TranscribeFileAsync(
            string filePath,
            string? language = null,
            string? prompt = null,
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Файл не найден: {filePath}");
            }

            var fileInfo = new FileInfo(filePath);
            EnsureSizeWithinLimit(fileInfo.Length, fileInfo.Name);

            await using var fileStream = File.OpenRead(filePath);
            return await TranscribeStreamAsync(fileStream, language, fileInfo.Name, prompt, cancellationToken);
        }

        private void EnsureSizeWithinLimit(long sizeBytes, string sourceName)
        {
            if (sizeBytes > _options.MaxFileSizeBytes)
            {
                var sizeMb = sizeBytes / (1024.0 * 1024.0);
                var maxMb = _options.MaxFileSizeBytes / (1024.0 * 1024.0);
                throw new ArgumentException(
                    $"Размер аудио '{sourceName}' ({sizeMb:F2} МБ) превышает максимально допустимый лимит Groq API ({maxMb:F0} МБ).",
                    nameof(sourceName));
            }
        }

        private static string GetMimeType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".wav" => "audio/wav",
                ".mp3" => "audio/mpeg",
                ".ogg" => "audio/ogg",
                ".m4a" => "audio/mp4",
                ".mp4" => "audio/mp4",
                ".webm" => "audio/webm",
                ".flac" => "audio/flac",
                _ => "application/octet-stream"
            };
        }
    }

    /// <summary>
    /// Полный отчет распознавания речи от Groq Whisper.
    /// </summary>
    public class GroqTranscriptionReport
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("language")]
        public string? Language { get; set; }

        [JsonPropertyName("duration")]
        public double Duration { get; set; }

        [JsonPropertyName("task")]
        public string? TaskName { get; set; }

        [JsonPropertyName("segments")]
        public List<GroqTranscriptionSegment>? Segments { get; set; }
    }

    /// <summary>
    /// Отдельный фрагмент (сегмент) распознанной речи с таймкодами.
    /// </summary>
    public class GroqTranscriptionSegment
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("seek")]
        public int Seek { get; set; }

        [JsonPropertyName("start")]
        public double Start { get; set; }

        [JsonPropertyName("end")]
        public double End { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("avg_logprob")]
        public double? AvgLogprob { get; set; }

        [JsonPropertyName("no_speech_prob")]
        public double? NoSpeechProb { get; set; }
    }
}
