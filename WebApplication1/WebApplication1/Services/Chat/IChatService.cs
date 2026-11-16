using WebApplication1.Models;

namespace WebApplication1.Services.Chat
{
    public record ChatExecutionResult(
        bool IsSuccess,
        string? Text = null,
        int StatusCode = 200,
        string? Error = null
    );

    /// <summary>
    /// Сервис оркестрации диалога и взаимодействия с ИИ.
    /// </summary>
    public interface IChatService
    {
        Task<ChatExecutionResult> ProcessMessageAsync(
            ChatRequest request,
            CancellationToken cancellationToken = default);
    }
}
