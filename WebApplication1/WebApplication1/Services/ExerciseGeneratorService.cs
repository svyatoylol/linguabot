using LinguaBot.Data;
using LinguaBot.Models;
using LinguaBot.Models.Analysis;
using LinguaBot.Models.Exercises;
using Microsoft.EntityFrameworkCore;

namespace LinguaBot.Services;

public class ExerciseGeneratorService
{
    private readonly ApplicationDbContext _db;
    private readonly IntelligentLanguageEngine _engine;
    private readonly LinguaDataStore _store;

    public ExerciseGeneratorService(
        ApplicationDbContext db,
        IntelligentLanguageEngine engine,
        LinguaDataStore store)
    {
        _db = db;
        _engine = engine;
        _store = store;
    }

    public async Task<List<ExerciseResponse>> GenerateAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        // 1. Проверяем, есть ли уже упражнения для этой сессии
        var existing = _store.Exercises.Values
            .Where(e => e.SessionId == sessionId)
            .OrderBy(e => e.Order)
            .ToList();

        if (existing.Count > 0)
        {
            return MapToResponses(existing);
        }

        var session = _store.Sessions.Values.FirstOrDefault(s => s.Id == sessionId);
        var sessionErrors = _store.Errors.Values
            .Where(e => e.SessionId == sessionId)
            .Select(e => new DetectedErrorResult
            {
                OriginalText = e.OriginalText,
                CorrectedText = e.CorrectedText,
                Explanation = e.Explanation,
                Category = e.Category,
                Severity = e.Severity
            })
            .ToList();

        string lang = session?.LanguageCode ?? "en";
        string level = session?.Level ?? "A1-A2";

        var generated = _engine.GenerateAdaptiveExercises(sessionId, lang, level, sessionErrors);

        foreach (var ex in generated)
        {
            _store.Exercises[ex.Id] = ex;
        }

        _store.SaveToDisk();

        // Попытка сохранения в БД при доступности PostgreSQL
        try
        {
            var dbSession = await _db.DialogueSessions.FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
            if (dbSession != null)
            {
                foreach (var ex in generated)
                {
                    _db.Exercises.Add(ex);
                }
                await _db.SaveChangesAsync(cancellationToken);
            }
        }
        catch
        {
            // Fallback в LinguaDataStore сохранён
        }

        return MapToResponses(generated);
    }

    public List<ExerciseResponse> GetPracticeExercises(string? category, string? level)
    {
        var query = _store.Exercises.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(category) && category != "all")
        {
            query = query.Where(e => e.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(level))
        {
            query = query.Where(e => e.Level.Equals(level, StringComparison.OrdinalIgnoreCase));
        }

        var list = query.OrderBy(e => e.Order).Take(15).ToList();
        if (list.Count == 0)
        {
            // Сгенерируем базовые задания для выбранной категории
            var baseExercises = _engine.GenerateAdaptiveExercises(Guid.Empty, "en", level ?? "A1-A2", new List<DetectedErrorResult>());
            foreach (var ex in baseExercises)
            {
                _store.Exercises[ex.Id] = ex;
            }
            _store.SaveToDisk();
            list = baseExercises;
        }

        return MapToResponses(list);
    }

    private static List<ExerciseResponse> MapToResponses(List<Exercise> exercises)
    {
        return exercises.Select(e =>
        {
            List<string> options = new();
            try
            {
                options = System.Text.Json.JsonSerializer.Deserialize<List<string>>(e.Options) ?? new();
            }
            catch { }

            return new ExerciseResponse
            {
                Id = e.Id,
                SessionId = e.SessionId ?? Guid.Empty,
                Type = e.Type,
                Category = e.Category,
                Level = e.Level,
                Question = e.Question,
                Options = options,
                Explanation = e.Explanation,
                AudioText = e.AudioText,
                Order = e.Order
            };
        }).ToList();
    }
}
