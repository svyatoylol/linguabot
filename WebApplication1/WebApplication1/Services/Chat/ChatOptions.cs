namespace WebApplication1.Services.Chat
{
    /// <summary>
    /// Настройки подключения к Google Gemini API.
    /// </summary>
    public class GeminiOptions
    {
        public const string SectionName = "Gemini";

        public string ApiKey { get; set; } = string.Empty;

        public string Model { get; set; } = "gemini-2.5-flash";

        public string EndpointUrl => $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent";
    }
}
