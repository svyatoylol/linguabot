using System.Net.Http.Json;
using System.Text.Json;

namespace LinguaBot.Services;

public class GeminiService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IntelligentLanguageEngine _engine;

    public GeminiService(
        HttpClient httpClient,
        IConfiguration configuration,
        IntelligentLanguageEngine engine)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _engine = engine;
    }

    public async Task<string> GenerateResponseAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        string? apiKey = _configuration["Gemini:ApiKey"];
        string model = _configuration["Gemini:Model"] ?? "gemini-3.8-flash";

        // Если ключ не задан или пуст — мгновенный переход на локальный интеллектуальный движок
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return GenerateLocalFallback(prompt);
        }

        try
        {
            var requestBody = new
            {
                system_instruction = new
                {
                    parts = new object[]
                    {
                        new { text = "You are LinguaBot, a friendly and supportive language tutor. Help users practice foreign languages. Respond naturally, concisely (1-3 sentences), keep the conversation flowing, ask engaging follow-up questions, and adapt your language to the user's level." }
                    }
                },
                contents = new object[]
                {
                    new
                    {
                        role = "user",
                        parts = new object[]
                        {
                            new { text = prompt }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.7,
                    maxOutputTokens = 300
                }
            };

            string endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";
            using var message = new HttpRequestMessage(HttpMethod.Post, endpoint);
            message.Headers.Add("x-goog-api-key", apiKey);
            message.Content = JsonContent.Create(requestBody);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(2.8)); // Гарантия времени ответа ≤ 3 секунд по ТЗ

            using var response = await _httpClient.SendAsync(message, cts.Token);
            string responseBody = await response.Content.ReadAsStringAsync(cts.Token);

            if (response.IsSuccessStatusCode)
            {
                using JsonDocument document = JsonDocument.Parse(responseBody);
                var root = document.RootElement;

                // 1. Стандартный формат generateContent
                if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                {
                    var firstCand = candidates[0];
                    if (firstCand.TryGetProperty("content", out var content) &&
                        content.TryGetProperty("parts", out var parts))
                    {
                        var result = new System.Text.StringBuilder();
                        foreach (var part in parts.EnumerateArray())
                        {
                            if (part.TryGetProperty("text", out var text))
                            {
                                result.Append(text.GetString());
                            }
                        }

                        if (result.Length > 0)
                            return result.ToString().Trim();
                    }
                }

                // 2. Формат interactions preview
                if (root.TryGetProperty("steps", out var steps))
                {
                    var result = new System.Text.StringBuilder();
                    foreach (var step in steps.EnumerateArray())
                    {
                        if (step.TryGetProperty("type", out var type) && type.GetString() == "model_output" &&
                            step.TryGetProperty("content", out var content))
                        {
                            foreach (var part in content.EnumerateArray())
                            {
                                if (part.TryGetProperty("text", out var text))
                                {
                                    result.Append(text.GetString());
                                }
                            }
                        }
                    }

                    if (result.Length > 0)
                        return result.ToString().Trim();
                }
            }
        }
        catch
        {
            // Fallback при сетевом таймауте (>2.8с), недоступности API или офлайн-режиме
        }

        return GenerateLocalFallback(prompt);
    }

    private string GenerateLocalFallback(string prompt)
    {
        // Извлекаем контекст из промпта
        string scenario = "introduction";
        string lang = "en";
        string level = "A1-A2";

        if (prompt.Contains("Dialogue scenario: "))
        {
            var match = System.Text.RegularExpressions.Regex.Match(prompt, @"Dialogue scenario:\s*([^\r\n]+)");
            if (match.Success) scenario = match.Groups[1].Value.Trim();
        }
        if (prompt.Contains("Learning language: "))
        {
            var match = System.Text.RegularExpressions.Regex.Match(prompt, @"Learning language:\s*([^\r\n]+)");
            if (match.Success) lang = match.Groups[1].Value.Trim();
        }
        if (prompt.Contains("User level: "))
        {
            var match = System.Text.RegularExpressions.Regex.Match(prompt, @"User level:\s*([^\r\n]+)");
            if (match.Success) level = match.Groups[1].Value.Trim();
        }

        // Последнее сообщение пользователя
        string lastUserText = "";
        var userMatches = System.Text.RegularExpressions.Regex.Matches(prompt, @"User:\s*([^\r\n]+)");
        if (userMatches.Count > 0)
        {
            lastUserText = userMatches[^1].Groups[1].Value.Trim();
        }

        bool isNoCheatSheet = prompt.Contains("NoCheatSheet") || prompt.Contains("измененный вопрос");

        return _engine.GenerateDialogueReply(scenario, lang, level, lastUserText, userMatches.Count, isNoCheatSheet);
    }
}
