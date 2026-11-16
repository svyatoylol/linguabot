namespace WebApplication1.Models
{
    /// <summary>
    /// Запрос на синтез речи (Text-to-Speech).
    /// </summary>
    public record TtsRequest(
        string Text,
        string? Lang = null
    );
}
