
using System.Text.Json;
using LinguaBot.Models;

namespace LinguaBot.Services;

public class PlacementTestGeneratorService
{
    private readonly GeminiService _gemini;

    public PlacementTestGeneratorService(GeminiService gemini)
    {
        _gemini = gemini;
    }

    public async Task<List<GeneratedPlacementQuestion>> GenerateAsync(
        string languageCode,
        string selectedLevel,
        CancellationToken cancellationToken)
    {
        string language = languageCode switch
        {
            "en" => "English",
            "pl" => "Polish",
            "ru" => "Russian",
            _ => throw new InvalidOperationException(
                "Unsupported language."
            )
        };

        var prompt = $$"""
            Create a placement test for a student learning {{language}}.

            Generate exactly 12 multiple-choice questions:
            - Questions 1-4: A1-A2
            - Questions 5-8: B1-B2
            - Questions 9-12: C1

            Include a mix of grammar, vocabulary, and reading comprehension.

            Each question must have exactly 4 answer options and one correct answer.

            Return ONLY a valid JSON array. Do not use Markdown or code blocks.

            Required JSON structure:
            [
              {
                "skill": "Grammar",
                "type": "MultipleChoice",
                "prompt": "Question text",
                "options": ["Option A", "Option B", "Option C", "Option D"],
                "correctAnswer": "Option A",
                "explanation": "Explanation in Russian"
              }
            ]

            All questions must be written in {{language}}.
            Explanations must be written in Russian.
            """;

        string response = await _gemini.GenerateResponseAsync(
            prompt,
            cancellationToken
        );

        string json = ExtractJsonArray(response);

        var questions =
            JsonSerializer.Deserialize<List<GeneratedPlacementQuestion>>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }
            );

        if (questions == null || questions.Count != 12)
        {
            throw new InvalidOperationException(
                "Gemini returned an invalid number of questions."
            );
        }

        foreach (var question in questions)
        {
            if (string.IsNullOrWhiteSpace(question.Prompt) ||
                string.IsNullOrWhiteSpace(question.CorrectAnswer) ||
                string.IsNullOrWhiteSpace(question.Explanation) ||
                question.Options == null ||
                question.Options.Count != 4)
            {
                throw new InvalidOperationException(
                    "Gemini generated an invalid question."
                );
            }

            if (!question.Options.Any(option =>
                Normalize(option) == Normalize(question.CorrectAnswer)))
            {
                throw new InvalidOperationException(
                    "Correct answer is missing from the options."
                );
            }

            if (question.Options
                .Select(Normalize)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() != 4)
            {
                throw new InvalidOperationException(
                    "Question contains duplicate options."
                );
            }
        }

        return questions;
    }

    private static string ExtractJsonArray(string response)
    {
        int start = response.IndexOf('[');
        int end = response.LastIndexOf(']');

        if (start < 0 || end <= start)
        {
            throw new InvalidOperationException(
                "Gemini did not return a JSON array."
            );
        }

        return response.Substring(start, end - start + 1);
    }

    private static string Normalize(string value)
    {
        return string.Join(
            " ",
            value.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries
            )
        ).Trim();
    }
}

public class GeneratedPlacementQuestion
{
    public string Skill { get; set; } = "Grammar";

    public string Type { get; set; } = "MultipleChoice";

    public string Prompt { get; set; } = "";

    public List<string> Options { get; set; } = new();

    public string CorrectAnswer { get; set; } = "";

    public string Explanation { get; set; } = "";
}
