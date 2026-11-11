using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace VoiceService
{
    public static class VoiceExtensions
    {
        /// <summary>
        /// Регистрация сервисов распознавания речи (Groq Whisper) и устойчивого HttpClient.
        /// </summary>
        public static IServiceCollection AddVoiceServices(this IServiceCollection services)
        {
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
                    // Таймауты под обработку речи Whisper
                    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(60);
                    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
                    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(120); // >= 2 * AttemptTimeout

                    // Retry при 429 от Groq
                    options.Retry.MaxRetryAttempts = 3;
                    options.Retry.Delay = TimeSpan.FromSeconds(2);
                    options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
                    options.Retry.UseJitter = true;
                });

            return services;
        }

        /// <summary>
        /// Подключение эндпоинта распознавания речи /api/voice.
        /// </summary>
        public static IEndpointRouteBuilder MapVoiceEndpoints(this IEndpointRouteBuilder app)
        {
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

            return app;
        }
    }
}
