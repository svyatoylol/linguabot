using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace VoiceService
{
    /// <summary>
    /// Реализация сервиса синтеза речи на базе локального легковесного движка Piper TTS.
    /// Поддерживает русский, польский, английский и любые другие языки, настроенные через ONNX-модели.
    /// </summary>
    public class PiperTtsService : ITtsService
    {
        private readonly TtsOptions _options;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<PiperTtsService> _logger;

        public PiperTtsService(
            IOptions<TtsOptions> options,
            IWebHostEnvironment environment,
            ILogger<PiperTtsService> logger)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<Stream> SynthesizeStreamAsync(string text, string? language = null, CancellationToken cancellationToken = default)
        {
            var bytes = await SynthesizeBytesAsync(text, language, cancellationToken);
            return new MemoryStream(bytes);
        }

        /// <inheritdoc />
        public async Task<byte[]> SynthesizeBytesAsync(string text, string? language = null, CancellationToken cancellationToken = default)
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"piper_{Guid.NewGuid():N}.wav");
            try
            {
                await SynthesizeToFileAsync(text, tempFile, language, cancellationToken);
                return await File.ReadAllBytesAsync(tempFile, cancellationToken);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch (Exception ex) { _logger.LogTrace(ex, "Не удалось удалить временный файл {Path}", tempFile); }
                }
            }
        }

        /// <inheritdoc />
        public async Task SynthesizeToFileAsync(string text, string outputFilePath, string? language = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Текст для озвучивания не может быть пустым.", nameof(text));

            var modelPath = ResolveModelPath(language);
            var exePath = ResolveExecutablePath();

            var outDir = Path.GetDirectoryName(outputFilePath);
            if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
            {
                Directory.CreateDirectory(outDir);
            }

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"--model \"{modelPath}\" --output_file \"{outputFilePath}\"",
                RedirectStandardInput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            _logger.LogInformation("Запуск синтеза речи Piper. Модель: {Model}, Язык: {Lang}", Path.GetFileName(modelPath), language ?? _options.DefaultLanguage);

            Process? process = null;
            try
            {
                process = Process.Start(psi);
            }
            catch (Win32Exception ex)
            {
                throw new InvalidOperationException(
                    $"Исполняемый файл Piper TTS ('{exePath}') не найден или не может быть запущен. " +
                    "Убедитесь, что Piper установлен на сервере (например, 'pip install piper-tts' или бинарник помещен в PATH), " +
                    "либо задайте корректный путь в секции конфигурации 'PiperTTS:ExecutablePath'.", ex);
            }

            if (process == null)
            {
                throw new InvalidOperationException($"Не удалось запустить процесс Piper TTS по пути '{exePath}'.");
            }

            using (process)
            {
                // Запуск асинхронного чтения ошибок, чтобы буфер stderr не переполнился
                var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

                // Передаем текст в поток stdin в кодировке UTF-8
                await using (var writer = new StreamWriter(process.StandardInput.BaseStream, new UTF8Encoding(false), leaveOpen: true))
                {
                    await writer.WriteLineAsync(text.AsMemory(), timeoutCts.Token);
                    await writer.FlushAsync(timeoutCts.Token);
                }
                process.StandardInput.Close();

                await process.WaitForExitAsync(timeoutCts.Token);
                var stderr = await stderrTask;

                if (process.ExitCode != 0)
                {
                    _logger.LogError("Ошибка процесса Piper TTS (ExitCode: {Code}): {Stderr}", process.ExitCode, stderr);
                    throw new InvalidOperationException($"Ошибка генерации Piper TTS (код {process.ExitCode}): {stderr.Trim()}");
                }

                if (!File.Exists(outputFilePath) || new FileInfo(outputFilePath).Length == 0)
                {
                    throw new InvalidOperationException($"Piper TTS завершился без ошибок, но аудиофайл '{outputFilePath}' пуст или отсутствует.");
                }

                _logger.LogInformation("Аудиофайл успешно синтезирован: {Path} ({Size} байт)", outputFilePath, new FileInfo(outputFilePath).Length);
            }
        }

        /// <summary>
        /// Поиск и валидация файла модели (.onnx) для запрошенного языка.
        /// </summary>
        private string ResolveModelPath(string? language)
        {
            var requestedLang = string.IsNullOrWhiteSpace(language)
                ? _options.DefaultLanguage
                : language.Trim().ToLowerInvariant();

            string? modelFile = null;

            // 1. Точное совпадение (например "ru", "pl", "en", "ru-ru")
            if (!_options.VoiceModels.TryGetValue(requestedLang, out modelFile))
            {
                // 2. Попытка укоротить код (например "ru-RU" -> "ru", "en_US" -> "en")
                var shortLang = requestedLang.Split(new[] { '-', '_' }, 2)[0];
                if (!_options.VoiceModels.TryGetValue(shortLang, out modelFile))
                {
                    // 3. Фолбэк на дефолтный язык
                    if (!_options.VoiceModels.TryGetValue(_options.DefaultLanguage, out modelFile))
                    {
                        throw new ArgumentException(
                            $"Язык '{language}' не настроен в конфигурации PiperTTS:VoiceModels, и дефолтная модель не найдена.");
                    }
                    _logger.LogWarning("Язык '{Lang}' не найден в маппинге моделей, использован язык по умолчанию '{DefaultLang}'", requestedLang, _options.DefaultLanguage);
                }
            }

            var baseDir = Path.IsPathRooted(_options.ModelsDirectory)
                ? _options.ModelsDirectory
                : Path.Combine(_environment.ContentRootPath, _options.ModelsDirectory);

            var fullModelPath = Path.IsPathRooted(modelFile)
                ? modelFile
                : Path.Combine(baseDir, modelFile);

            if (!File.Exists(fullModelPath))
            {
                throw new FileNotFoundException(
                    $"Файл модели Piper TTS не найден: '{fullModelPath}'. " +
                    $"Поместите файл модели ({Path.GetFileName(fullModelPath)}) и файл конфига ({Path.GetFileName(fullModelPath)}.json) в папку '{baseDir}'.",
                    fullModelPath);
            }

            return fullModelPath;
        }

        /// <summary>
        /// Разрешение пути к бинарнику piper.
        /// </summary>
        private string ResolveExecutablePath()
        {
            var exe = _options.ExecutablePath;
            if (string.IsNullOrWhiteSpace(exe))
            {
                exe = "piper";
            }

            if (!Path.IsPathRooted(exe))
            {
                var localCandidate = Path.Combine(_environment.ContentRootPath, exe);
                if (File.Exists(localCandidate))
                {
                    return localCandidate;
                }
            }

            return exe;
        }
    }
}
