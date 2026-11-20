namespace WebApplication1.Models
{
    /// <summary>
    /// Запрос пользователя к чату LinguaBot.
    /// </summary>
    public record ChatRequest(
        string Message,
        Guid? SessionId = null,
        string? Scenario = null,
        string? Lang = null,
        string? Level = null
    );
}
