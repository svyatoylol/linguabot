using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WebApplication1.Models;

namespace WebApplication1.Services.Chat
{
    public static class ChatExtensions
    {
        /// <summary>
        /// Регистрация сервисов контекста и чата LinguaBot.
        /// </summary>
        public static IServiceCollection AddChatServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<GeminiOptions>(options =>
            {
                configuration.GetSection(GeminiOptions.SectionName).Bind(options);

                if (string.IsNullOrWhiteSpace(options.ApiKey))
                {
                    options.ApiKey = configuration["Gemini:ApiKey"] ?? string.Empty;
                }

                var model = configuration["Gemini:Model"];
                if (!string.IsNullOrWhiteSpace(model))
                {
                    options.Model = model;
                }
            });

            services.AddSingleton<IConversationContextService, ConversationContextService>();
            services.AddHttpClient<IChatService, ChatService>();

            return services;
        }

        /// <summary>
        /// Подключение эндпоинта /api/chat.
        /// </summary>
        public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapPost("/api/chat", async (ChatRequest request, IChatService chatService, CancellationToken ct) =>
            {
                var result = await chatService.ProcessMessageAsync(request, ct);

                if (!result.IsSuccess)
                {
                    return Results.Json(new { text = result.Error }, statusCode: result.StatusCode);
                }

                return Results.Ok(new { text = result.Text });
            });

            return app;
        }
    }
}
