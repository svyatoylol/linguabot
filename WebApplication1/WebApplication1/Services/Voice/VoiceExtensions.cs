using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WebApplication1.Models;

namespace VoiceService
{
    public static class VoiceExtensions
    {
        /// <summary>
        /// Регистрация сервисов работы с речью (Groq Whisper STT + Piper TTS).
        /// </summary>
        public static IServiceCollection AddVoiceServices(this IServiceCollection services)
        {
            // 1. Настройка и регистрация Groq Whisper (STT)
            services.AddOptions<SttOptions>()
                .BindConfiguration(SttOptions.SectionName)
                .PostConfigure<IConfiguration>((options, config) =>
                {
                    // Легаси-фоллбэк для старых ключей из User Secrets
                    if (string.IsNullOrWhiteSpace(options.ApiKey))
                    {
                        options.ApiKey = config["Grok:GrokApiKey"]
                                      ?? config["Grok:ApiKey"]
                                      ?? config["STTSettings:ApiKey"]
                                      ?? string.Empty;
                    }
                })
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddHttpClient<ISttService, SttService>()
                .ConfigureHttpClient(c => c.Timeout = Timeout.InfiniteTimeSpan)
                .AddStandardResilienceHandler(options =>
                {
                    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(60);
                    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
                    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(120);

                    options.Retry.MaxRetryAttempts = 3;
                    options.Retry.Delay = TimeSpan.FromSeconds(2);
                    options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
                    options.Retry.UseJitter = true;
                });

            // 2. Настройка и регистрация Piper TTS
            services.AddOptions<TtsOptions>()
                .BindConfiguration(TtsOptions.SectionName);

            services.AddSingleton<ITtsService, PiperTtsService>();

            // 3. Регистрация голосового оркестратора (STT + Chat + TTS)
            services.AddScoped<IVoiceOrchestrator, VoiceOrchestrator>();

            return services;
        }

        /// <summary>
        /// Подключение эндпоинтов распознавания (/api/voice) и синтеза речи (/api/tts).
        /// </summary>
        public static IEndpointRouteBuilder MapVoiceEndpoints(this IEndpointRouteBuilder app)
        {
            // STT: Распознавание входящего аудио
            app.MapPost("/api/voice", async (
                IFormFile? audio,
                HttpContext context,
                string? lang,
                ISttService stt,
                IWebHostEnvironment env,
                CancellationToken ct) =>
            {
                var file = audio;
                if (file == null && context.Request.HasFormContentType && context.Request.Form.Files.Count > 0)
                {
                    file = context.Request.Form.Files[0];
                }

                if (file == null || file.Length == 0)
                    return Results.BadRequest(new { text = "Пустой файл." });

                try
                {
                    await using var stream = file.OpenReadStream();
                    var report = await stt.TranscribeStreamAsync(stream, lang, file.FileName, cancellationToken: ct);

                    return string.IsNullOrWhiteSpace(report?.Text)
                        ? Results.BadRequest(new { text = "Речь не распознана." })
                        : Results.Ok(new { text = report.Text.Trim() });
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { text = ex.Message });
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    var msg = env.IsDevelopment() ? $"Ошибка распознавания: {ex.Message}" : "Ошибка сервиса распознавания речи.";
                    return Results.Json(new { text = msg }, statusCode: 500);
                }
            }).DisableAntiforgery();

            // TTS: Синтез речи через GET-запрос (удобно для браузерного <audio src="...">)
            app.MapGet("/api/tts", async (
                string? text,
                string? lang,
                ITtsService tts,
                IWebHostEnvironment env,
                CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(text))
                    return Results.BadRequest(new { error = "Параметр 'text' обязателен." });

                try
                {
                    var stream = await tts.SynthesizeStreamAsync(text, lang, ct);
                    return Results.File(stream, "audio/wav", fileDownloadName: "speech.wav");
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
                catch (FileNotFoundException ex)
                {
                    return Results.NotFound(new { error = ex.Message });
                }
                catch (Exception ex)
                {
                    var msg = env.IsDevelopment() ? $"Ошибка синтеза речи: {ex.Message}" : "Ошибка сервиса синтеза речи.";
                    return Results.Json(new { error = msg }, statusCode: 500);
                }
            });

            // TTS: Синтез речи через POST-запрос с телом JSON
            app.MapPost("/api/tts", async (
                TtsRequest request,
                ITtsService tts,
                IWebHostEnvironment env,
                CancellationToken ct) =>
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Text))
                    return Results.BadRequest(new { error = "Поле 'text' не может быть пустым." });

                try
                {
                    var stream = await tts.SynthesizeStreamAsync(request.Text, request.Lang, ct);
                    return Results.File(stream, "audio/wav");
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
                catch (FileNotFoundException ex)
                {
                    return Results.NotFound(new { error = ex.Message });
                }
                catch (Exception ex)
                {
                    var msg = env.IsDevelopment() ? $"Ошибка синтеза речи: {ex.Message}" : "Ошибка сервиса синтеза речи.";
                    return Results.Json(new { error = msg }, statusCode: 500);
                }
            });

            // Voice Mode / Voice Dialog: Сквозное голосовое общение (STT -> Gemini Chat -> Piper TTS)
            var handleVoiceDialog = async (
                IFormFile? audio,
                HttpContext context,
                IVoiceOrchestrator orchestrator,
                IWebHostEnvironment env,
                CancellationToken ct) =>
            {
                var file = audio;
                if (file == null && context.Request.HasFormContentType && context.Request.Form.Files.Count > 0)
                {
                    file = context.Request.Form.Files[0];
                }

                if (file == null || file.Length == 0)
                {
                    return Results.BadRequest(new { error = "Аудиофайл не передан или пуст." });
                }

                int? sessionId = null;
                string? scenario = null;
                string? lang = null;
                string? level = null;

                if (context.Request.HasFormContentType)
                {
                    var form = context.Request.Form;
                    if (form.TryGetValue("sessionId", out var sIdStr) && int.TryParse(sIdStr, out var sId))
                    {
                        sessionId = sId;
                    }
                    if (form.TryGetValue("scenario", out var sc)) scenario = sc;
                    if (form.TryGetValue("lang", out var l)) lang = l;
                    if (form.TryGetValue("level", out var lvl)) level = lvl;
                }

                try
                {
                    await using var stream = file.OpenReadStream();
                    var result = await orchestrator.ProcessVoiceAsync(
                        stream,
                        file.FileName,
                        sessionId,
                        scenario,
                        lang,
                        level,
                        ct);

                    if (!result.IsSuccess)
                    {
                        return Results.Json(new
                        {
                            error = result.Error,
                            userText = result.UserText
                        }, statusCode: result.StatusCode);
                    }

                    return Results.Ok(new
                    {
                        userText = result.UserText,
                        aiText = result.AiText,
                        audioBase64 = result.AudioBase64,
                        audioMimeType = result.AudioMimeType,
                        detectedLanguage = result.DetectedLanguage,
                        pronunciationScore = result.PronunciationScore
                    });
                }
                catch (Exception ex)
                {
                    var msg = env.IsDevelopment() ? $"Ошибка оркестратора: {ex.Message}" : "Внутренняя ошибка сервиса голосового диалога.";
                    return Results.Json(new { error = msg }, statusCode: 500);
                }
            };

            app.MapPost("/api/voice/dialog", handleVoiceDialog).DisableAntiforgery();
            app.MapPost("/api/voice/mode", handleVoiceDialog).DisableAntiforgery();

            return app;
        }
    }
}
