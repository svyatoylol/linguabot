
using System.Text;
using System.Text.Json;
using LinguaBot.Data;
using LinguaBot.Models;
using LinguaBot.Models.Analysis;
using Microsoft.EntityFrameworkCore;

namespace LinguaBot.Services;

public class DialogueAnalysisService
{
    private readonly ApplicationDbContext _db;
    private readonly GeminiService _gemini;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HashSet<string> AllowedCategories = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "Grammar",
        "Vocabulary",
        "Spelling",
        "WordOrder",
        "Other"
    };

    public DialogueAnalysisService(
        ApplicationDbContext db,
        GeminiService gemini)
    {
        _db = db;
        _gemini = gemini;
    }

    public async Task<DialogueAnalysis> AnalyzeAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        // Проверяем, не анализировали ли эту сессию раньше.
        var existingAnalysis = await _db.DialogueAnalyses
            .FirstOrDefaultAsync(
                a => a.SessionId == sessionId,
                cancellationToken
            );

        if (existingAnalysis != null)
            return existingAnalysis;

        // Загружаем сессию, пользователя и историю сообщений.
        var session = await _db.DialogueSessions
            .Include(s => s.User)
                .ThenInclude(u => u.Settings)
            .Include(s => s.Messages)
            .FirstOrDefaultAsync(
                s => s.Id == sessionId,
                cancellationToken
            );

        if (session == null)
        {
            throw new InvalidOperationException(
                "Dialogue session was not found."
            );
        }

        var messages = session.Messages
            .OrderBy(m => m.CreatedAt)
            .ToList();

        if (!messages.Any(m => m.Role == "user"))
        {
            throw new InvalidOperationException(
                "Cannot analyze a dialogue without user messages."
            );
        }

        // Формируем историю диалога.
        var conversation = new StringBuilder();

        foreach (var message in messages)
        {
            string role = message.Role == "user"
                ? "User"
                : "Assistant";

            conversation.AppendLine(
                $"{role}: {message.Content}"
            );
        }

        string learningLanguage =
            session.User.Settings.LearningLanguage;

        string currentLevel =
            session.User.Settings.Level;

        // Формируем инструкцию для Gemini.
        var prompt = new StringBuilder();

        prompt.AppendLine(
            "You are an expert foreign language teacher."
        );

        prompt.AppendLine(
            "Analyze the user's language performance in the conversation."
        );

        prompt.AppendLine(
            $"The language being learned is: {learningLanguage}."
        );

        prompt.AppendLine(
            $"The user's current level is: {currentLevel}."
        );

        prompt.AppendLine();

        prompt.AppendLine("ANALYSIS REQUIREMENTS:");

        prompt.AppendLine(
            "1. Identify meaningful language mistakes made by the user."
        );

        prompt.AppendLine(
            "2. Ignore mistakes made by the assistant."
        );

        prompt.AppendLine(
            "3. Do not classify correct informal language as an error."
        );

        prompt.AppendLine(
            "4. Provide the original phrase and its corrected version."
        );

        prompt.AppendLine(
            "5. Explain each correction in the user's native language."
        );

        prompt.AppendLine(
            "6. Identify the user's strengths."
        );

        prompt.AppendLine(
            "7. Recommend a realistic language proficiency level."
        );

        prompt.AppendLine(
            "8. Do not invent mistakes that are not present in the conversation."
        );

        prompt.AppendLine(
            "9. If there are no mistakes, return an empty errors array."
        );

        prompt.AppendLine();

        prompt.AppendLine("ERROR CATEGORIES:");

        prompt.AppendLine(
            "Grammar, Vocabulary, Spelling, WordOrder, Other."
        );

        prompt.AppendLine(
            "Severity must be an integer from 1 to 3:"
        );

        prompt.AppendLine(
            "1 = minor mistake, 2 = noticeable mistake, 3 = serious mistake."
        );

        prompt.AppendLine();

        prompt.AppendLine("OUTPUT FORMAT:");

        prompt.AppendLine(
            "Return ONLY valid JSON. Do not use Markdown code blocks."
        );

        prompt.AppendLine(
            """
            {
              "overallFeedback": "General feedback",
              "strengths": "User's strengths",
              "recommendedLevel": "A2",
              "errors": [
                {
                  "originalText": "Incorrect phrase",
                  "correctedText": "Correct phrase",
                  "explanation": "Explanation",
                  "category": "Grammar",
                  "severity": 2
                }
              ]
            }
            """
        );

        prompt.AppendLine();
        prompt.AppendLine("CONVERSATION:");
        prompt.AppendLine(conversation.ToString());

        // Получаем анализ от Gemini.
        string response = await _gemini.GenerateResponseAsync(
            prompt.ToString(),
            cancellationToken
        );

        string json = ExtractJson(response);

        DialogueAnalysisResult? result;

        try
        {
            result = JsonSerializer.Deserialize<DialogueAnalysisResult>(
                json,
                JsonOptions
            );
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                "Gemini returned invalid analysis JSON.",
                ex
            );
        }

        if (result == null)
        {
            throw new InvalidOperationException(
                "Gemini returned an empty analysis."
            );
        }

        // Сохраняем общий анализ.
        var analysis = new DialogueAnalysis
        {
            SessionId = session.Id,

            OverallFeedback = Limit(
                result.OverallFeedback,
                5000
            ),

            Strengths = Limit(
                result.Strengths,
                3000
            ),

            RecommendedLevel = Limit(
                result.RecommendedLevel,
                20
            )
        };

        _db.DialogueAnalyses.Add(analysis);

        // Сохраняем обнаруженные ошибки.
        foreach (var error in result.Errors.Take(50))
        {
            if (string.IsNullOrWhiteSpace(error.OriginalText) ||
                string.IsNullOrWhiteSpace(error.CorrectedText))
            {
                continue;
            }

            string category = AllowedCategories.Contains(error.Category)
                ? error.Category
                : "Other";

            var detectedError = new DetectedError
            {
                SessionId = session.Id,

                OriginalText = Limit(
                    error.OriginalText,
                    1000
                ),

                CorrectedText = Limit(
                    error.CorrectedText,
                    1000
                ),

                Explanation = Limit(
                    error.Explanation,
                    3000
                ),

                Category = category,

                Severity = Math.Clamp(
                    error.Severity,
                    1,
                    3
                )
            };

            _db.DetectedErrors.Add(detectedError);
        }

        // Одна операция сохранения для анализа и всех ошибок.
        await _db.SaveChangesAsync(cancellationToken);

        return analysis;
    }

    private static string ExtractJson(string response)
    {
        string json = response.Trim();

        if (json.StartsWith("```"))
        {
            int firstNewLine = json.IndexOf('\n');

            if (firstNewLine >= 0)
                json = json[(firstNewLine + 1)..];

            int lastFence = json.LastIndexOf("```");

            if (lastFence >= 0)
                json = json[..lastFence];
        }

        return json.Trim();
    }

    private static string Limit(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        string trimmed = value.Trim();

        return trimmed.Length <= maxLength
            ? trimmed
            : trimmed[..maxLength];
    }
}
