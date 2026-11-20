using LinguaBot.Data;
using LinguaBot.Models;
using LinguaBot.Models.Analysis;
using Microsoft.EntityFrameworkCore;

namespace LinguaBot.Services;

public class DialogueAnalysisService
{
    private readonly ApplicationDbContext _db;
    private readonly IntelligentLanguageEngine _engine;
    private readonly LinguaDataStore _store;

    public DialogueAnalysisService(
        ApplicationDbContext db,
        IntelligentLanguageEngine engine,
        LinguaDataStore store)
    {
        _db = db;
        _engine = engine;
        _store = store;
    }

    public async Task<DialogueAnalysis> AnalyzeAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        // 1. Проверяем хранилище
        if (_store.Analyses.TryGetValue(sessionId, out var cached))
        {
            return cached;
        }

        // 2. Ищем сессию в store или db
        var session = _store.Sessions.Values.FirstOrDefault(s => s.Id == sessionId);
        var messages = _store.Messages.Values
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .Select(m => (m.Role, m.Content))
            .ToList();

        string scenarioId = session?.Scenario ?? "introduction";
        string lang = session?.LanguageCode ?? "en";
        string level = session?.Level ?? "A1-A2";

        // Анализируем диалог движком
        var result = _engine.AnalyzeDialogue(scenarioId, lang, level, messages);

        var analysis = new DialogueAnalysis
        {
            SessionId = sessionId,
            OverallFeedback = result.OverallFeedback,
            Strengths = result.Strengths,
            RecommendedLevel = result.RecommendedLevel,
            ErrorCount = result.Errors.Count,
            CreatedAt = DateTime.UtcNow
        };

        _store.Analyses[analysis.SessionId] = analysis;

        // Сохраняем выявленные ошибки
        foreach (var err in result.Errors)
        {
            var de = new DetectedError
            {
                SessionId = sessionId,
                OriginalText = err.OriginalText,
                CorrectedText = err.CorrectedText,
                Explanation = err.Explanation,
                Category = err.Category,
                Severity = err.Severity,
                CreatedAt = DateTime.UtcNow
            };
            _store.Errors[de.Id] = de;
        }

        // Проверяем и начисляем достижения
        if (session != null)
        {
            _store.CheckAndAwardAchievements(session.UserId);
        }

        _store.SaveToDisk();

        // Попытка асинхронной записи в БД, если PostgreSQL доступен
        try
        {
            var dbSession = await _db.DialogueSessions.FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
            if (dbSession != null)
            {
                var existing = await _db.DialogueAnalyses.FirstOrDefaultAsync(a => a.SessionId == sessionId, cancellationToken);
                if (existing == null)
                {
                    _db.DialogueAnalyses.Add(analysis);
                    await _db.SaveChangesAsync(cancellationToken);
                }
            }
        }
        catch
        {
            // Fallback в LinguaDataStore уже выполнен
        }

        return analysis;
    }
}
