
using System.Text;
using System.Text.Json;
using LinguaBot.Data;
using LinguaBot.Models;
using LinguaBot.Models.Exercises;
using Microsoft.EntityFrameworkCore;

namespace LinguaBot.Services;

public class ExerciseGeneratorService
{
    private readonly ApplicationDbContext _db;
    private readonly GeminiService _gemini;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HashSet<string> AllowedTypes = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "MultipleChoice",
        "FillInTheBlank",
        "Translation"
    };

    public ExerciseGeneratorService(
        ApplicationDbContext db,
        GeminiService gemini)
    {
        _db = db;
        _gemini = gemini;
    }

    public async Task<List<ExerciseResponse>> GenerateAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await _db.DialogueSessions
            .AsNoTracking()
            .Include(s => s.User)
                .ThenInclude(u => u.Settings)
            .Include(s => s.DetectedErrors)
            .FirstOrDefaultAsync(
                s => s.Id == sessionId,
                cancellationToken
            );

        if (session == null)
            throw new InvalidOperationException("Dialogue session was not found.");

        if (session.Status != "Completed")
            throw new InvalidOperationException("Only completed dialogues can generate exercises.");

        // Если упражнения уже существуют, возвращаем их.
        var existingExercises = await _db.Exercises
            .AsNoTracking()
            .Where(e => e.SessionId == sessionId)
            .OrderBy(e => e.Order)
            .ToListAsync(cancellationToken);

        if (existingExercises.Count > 0)
            return MapToResponses(existingExercises);

        // Пока адаптивные упражнения строятся на обнаруженных ошибках.
        if (session.DetectedErrors.Count == 0)
            return new List<ExerciseResponse>();

        var errors = session.DetectedErrors
            .OrderByDescending(e => e.Severity)
            .Take(10)
            .Select(e => new
            {
                e.Id,
                e.OriginalText,
                e.CorrectedText,
                e.Explanation,
                e.Category,
                e.Severity
            })
            .ToList();

        string errorsJson = JsonSerializer.Serialize(errors);

        var prompt = new StringBuilder();

        prompt.AppendLine("You are an expert language teacher.");
        prompt.AppendLine("Create adaptive exercises based on the user's actual language mistakes.");
        prompt.AppendLine();
        prompt.AppendLine($"Learning language: {session.User.Settings.LearningLanguage}");
        prompt.AppendLine($"Native language: {session.User.Settings.NativeLanguage}");
        prompt.AppendLine($"Current level: {session.User.Settings.Level}");
        prompt.AppendLine();
        prompt.AppendLine("REQUIREMENTS:");
        prompt.AppendLine("1. Create 1-2 exercises for each provided mistake.");
        prompt.AppendLine("2. Focus on understanding and correcting the underlying mistake.");
        prompt.AppendLine("3. Adapt difficulty to the user's current level.");
        prompt.AppendLine("4. Use clear, unambiguous questions.");
        prompt.AppendLine("5. Write questions and answer options in the learning language.");
        prompt.AppendLine("6. Write explanations in the user's native language.");
        prompt.AppendLine("7. Do not invent unrelated mistakes.");
        prompt.AppendLine("8. Include the exact DetectedErrorId associated with each exercise.");
        prompt.AppendLine("9. Generate no more than 12 exercises in total.");
        prompt.AppendLine();
        prompt.AppendLine("ALLOWED EXERCISE TYPES:");
        prompt.AppendLine("- MultipleChoice: provide 3-4 answer options.");
        prompt.AppendLine("- FillInTheBlank: provide no options.");
        prompt.AppendLine("- Translation: provide no options.");
        prompt.AppendLine();
        prompt.AppendLine("OUTPUT FORMAT:");
        prompt.AppendLine("Return ONLY valid JSON. Do not use Markdown code blocks.");
        prompt.AppendLine(
            """
            {
              "exercises": [
                {
                  "type": "MultipleChoice",
                  "question": "Choose the correct sentence.",
                  "options": [
                    "I am a student.",
                    "I is a student.",
                    "I are a student."
                  ],
                  "correctAnswer": "I am a student.",
                  "explanation": "После I используется форма am.",
                  "detectedErrorId": "00000000-0000-0000-0000-000000000000"
                }
              ]
            }
            """
        );
        prompt.AppendLine();
        prompt.AppendLine("USER MISTAKES:");
        prompt.AppendLine(errorsJson);

        string response = await _gemini.GenerateResponseAsync(
            prompt.ToString(),
            cancellationToken
        );

        string json = ExtractJson(response);

        ExerciseGenerationResult? result;

        try
        {
            result = JsonSerializer.Deserialize<ExerciseGenerationResult>(
                json,
                JsonOptions
            );
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                "Gemini returned invalid exercise JSON.",
                ex
            );
        }

        if (result == null)
            throw new InvalidOperationException("Gemini returned an empty exercise result.");

        var validErrorIds = session.DetectedErrors
            .Select(e => e.Id)
            .ToHashSet();

        var exercises = new List<Exercise>();

        foreach (var generated in result.Exercises.Take(12))
        {
            if (!AllowedTypes.Contains(generated.Type))
                continue;

            if (string.IsNullOrWhiteSpace(generated.Question) ||
                string.IsNullOrWhiteSpace(generated.CorrectAnswer))
                continue;

            if (!generated.DetectedErrorId.HasValue ||
                !validErrorIds.Contains(generated.DetectedErrorId.Value))
                continue;

            string type = AllowedTypes.First(
                t => string.Equals(
                    t,
                    generated.Type,
                    StringComparison.OrdinalIgnoreCase
                )
            );

            var options = generated.Options
                .Where(o => !string.IsNullOrWhiteSpace(o))
                .Select(o => Limit(o, 500))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToList();

            string correctAnswer = Limit(generated.CorrectAnswer, 500);

            if (type == "MultipleChoice")
            {
                if (options.Count < 2 || options.Count > 5)
                    continue;

                if (!options.Contains(
                    correctAnswer,
                    StringComparer.OrdinalIgnoreCase))
                    continue;
            }
            else
            {
                options.Clear();
            }

            exercises.Add(new Exercise
            {
                SessionId = sessionId,
                DetectedErrorId = generated.DetectedErrorId,
                Type = type,
                Question = Limit(generated.Question, 1000),
                Options = JsonSerializer.Serialize(options),
                CorrectAnswer = correctAnswer,
                Explanation = Limit(generated.Explanation, 2000),
                Order = exercises.Count + 1
            });
        }

        if (exercises.Count == 0)
        {
            throw new InvalidOperationException(
                "Gemini did not generate any valid exercises."
            );
        }

        _db.Exercises.AddRange(exercises);

        await _db.SaveChangesAsync(cancellationToken);

        return MapToResponses(exercises);
    }

    private static List<ExerciseResponse> MapToResponses(
        IEnumerable<Exercise> exercises)
    {
        return exercises.Select(e => new ExerciseResponse
        {
            Id = e.Id,
            Type = e.Type,
            Question = e.Question,
            Options = DeserializeOptions(e.Options),
            Order = e.Order
        }).ToList();
    }

    private static List<string> DeserializeOptions(string options)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(options)
                ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
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
