
using System.Net.Http.Json;
using System.Text.Json;

namespace LinguaBot.Services;

public class GeminiService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public GeminiService(
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<string> GenerateResponseAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        string? apiKey = _configuration["Gemini:ApiKey"];
        string model = _configuration["Gemini:Model"]
            ?? "gemini-3.8-flash";

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Gemini API key is not configured.");
        }

        var request = new
        {
            model,
            input = prompt,
            system_instruction =
                "You are LinguaBot, a friendly language tutor. " +
                "Help users practice foreign languages. " +
                "Respond naturally and adapt your language to the user's level.",
            store = false
        };

        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            "https://generativelanguage.googleapis.com/v1beta/interactions"
        );

        message.Headers.Add("x-goog-api-key", apiKey);
        message.Content = JsonContent.Create(request);

        using var response = await _httpClient.SendAsync(
            message,
            cancellationToken
        );

        string responseBody = await response.Content.ReadAsStringAsync(
            cancellationToken
        );

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Gemini API error ({(int)response.StatusCode}): " +
                responseBody
            );
        }

        using JsonDocument document = JsonDocument.Parse(responseBody);

        var root = document.RootElement;

        if (!root.TryGetProperty("steps", out var steps))
        {
            throw new InvalidOperationException(
                "Gemini response does not contain steps."
            );
        }

        var result = new System.Text.StringBuilder();

        foreach (var step in steps.EnumerateArray())
        {
            if (!step.TryGetProperty("type", out var type) ||
                type.GetString() != "model_output")
            {
                continue;
            }

            if (!step.TryGetProperty("content", out var content))
                continue;

            foreach (var part in content.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var text))
                {
                    result.Append(text.GetString());
                }
            }
        }

        if (result.Length == 0)
        {
            throw new InvalidOperationException(
                "Gemini returned an empty response."
            );
        }

        return result.ToString();
    }
}
