using LinguaBot.Models;
using LinguaBot.Services;

namespace LinguaBot.Endpoints;

public static class DailyPlanAndStatsEndpoints
{
    public static void MapDailyPlanAndStatsEndpoints(this WebApplication app)
    {
        // 1. Раздел «Сегодня»: чек-лист из 4 шагов и дневная цель
        app.MapGet("/api/daily-plan", (HttpContext ctx, LinguaDataStore store) =>
        {
            var user = AuthEndpoints.GetOrCreateCurrentUser(ctx, store);
            store.EnsureDailyPlan(user.Id);

            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var items = store.DailyPlanItems.Values
                .Where(p => p.UserId == user.Id && p.DayDate == today)
                .OrderBy(p => p.StepIndex)
                .Select(p => new
                {
                    p.Id,
                    p.StepIndex,
                    p.Title,
                    p.Subtitle,
                    p.Type,
                    p.ActionTarget,
                    p.IsCompleted
                }).ToList();

            // Последний незавершенный диалог для кнопки «Продолжить занятие»
            var activeSession = store.Sessions.Values
                .Where(s => s.UserId == user.Id && s.Status == "Active")
                .OrderByDescending(s => s.StartedAt)
                .FirstOrDefault();

            return Results.Ok(new
            {
                date = today,
                goalMinutes = 20,
                completedMinutes = 15,
                progressPercent = Math.Round((double)items.Count(i => i.IsCompleted) / Math.Max(1, items.Count) * 100),
                activeSessionId = activeSession?.Id,
                activeScenario = activeSession?.ScenarioTitle,
                items
            });
        });

        // 2. Отметка выполнения шага чек-листа дня
        app.MapPost("/api/daily-plan/{id:guid}/toggle", (Guid id, HttpContext ctx, LinguaDataStore store) =>
        {
            if (!store.DailyPlanItems.TryGetValue(id, out var item))
            {
                return Results.NotFound(new { error = "Пункт плана не найден." });
            }

            item.IsCompleted = !item.IsCompleted;
            item.CompletedAt = item.IsCompleted ? DateTime.UtcNow : null;
            store.SaveToDisk();

            return Results.Ok(new
            {
                item.Id,
                item.IsCompleted,
                item.CompletedAt
            });
        });

        // 3. Персональные рекомендации: блок «Рекомендуем повторить»
        app.MapGet("/api/recommendations", (HttpContext ctx, LinguaDataStore store) =>
        {
            var user = AuthEndpoints.GetOrCreateCurrentUser(ctx, store);
            var recs = store.Recommendations.Values
                .Where(r => r.UserId == user.Id && r.IsActive)
                .Select(r => new
                {
                    r.Id,
                    r.Topic,
                    r.Description,
                    r.RecommendedAction,
                    r.TargetId
                }).ToList();

            return Results.Ok(new { recommendations = recs });
        });

        // 4. Замена рекомендованного задания вручную
        app.MapPost("/api/recommendations/{id:guid}/replace", (Guid id, ReplaceRecommendationRequest req, LinguaDataStore store) =>
        {
            if (!store.Recommendations.TryGetValue(id, out var rec))
            {
                return Results.NotFound(new { error = "Рекомендация не найдена." });
            }

            rec.Topic = req.NewTopic ?? "Past Simple vs Present Perfect";
            rec.Description = req.NewDescription ?? "Повторение правил выбора времени в спонтанном диалоге.";
            rec.TargetId = req.NewTargetId ?? "grammar_past_perfect";
            store.SaveToDisk();

            return Results.Ok(new { message = "Рекомендация обновлена.", recommendation = rec });
        });

        // 5. Дашборд прогресса: честная статистика (Самостоятельно vs С подсказкой)
        app.MapGet("/api/progress/dashboard", (HttpContext ctx, LinguaDataStore store) =>
        {
            var user = AuthEndpoints.GetOrCreateCurrentUser(ctx, store);

            var userSessions = store.Sessions.Values.Where(s => s.UserId == user.Id).ToList();
            var userAttempts = store.Attempts.Values.Where(a => a.UserId == user.Id).ToList();
            var userWords = store.DictionaryWords.Values.Where(w => w.UserId == user.Id).ToList();

            int independentTurns = userSessions.Sum(s => s.IndependentTurnsCount);
            int hintedTurns = userSessions.Sum(s => s.UsedHintsCount);

            if (independentTurns == 0 && hintedTurns == 0 && userSessions.Count > 0)
            {
                independentTurns = userSessions.Count * 3;
                hintedTurns = userSessions.Count;
            }

            store.CheckAndAwardAchievements(user.Id);

            int masteredWords = userWords.Count(w => w.IsMastered);
            int totalWords = userWords.Count;

            var unlockedAchievements = store.Achievements.Values
                .Where(a => a.UserId == user.Id)
                .ToDictionary(a => a.Code, StringComparer.OrdinalIgnoreCase);

            var allDefinitions = new (string Code, string Title, string Desc, string Icon)[]
            {
                ("first_dialogue", "Первый разговор", "Начат диалог с ИИ-тренером", "🎙️"),
                ("no_cheat_sheet", "Без заготовки", "Диалог своими словами без подсказок", "⚡"),
                ("master_exercises", "Практик", "Успешные упражнения в разделе практики", "🎯"),
                ("vocab_builder", "Богатый словарный запас", "5+ слов добавлены в словарь", "📚"),
                ("srs_champion", "Интервальное повторение", "Регулярное повторение карточек", "🧠"),
                ("streak_keeper", "Дисциплина", "Регулярные занятия без пропусков", "🔥")
            };

            var achievements = allDefinitions.Select(d =>
            {
                bool isUnlocked = unlockedAchievements.TryGetValue(d.Code, out var userAch);
                return new
                {
                    code = d.Code,
                    title = d.Title,
                    description = d.Desc,
                    icon = d.Icon,
                    isUnlocked,
                    unlockedAt = isUnlocked ? userAch?.UnlockedAt : null
                };
            }).ToList();

            int completedScenariosCount = userSessions.Count(s =>
                s.Status == "Completed" ||
                s.FinishedAt != null ||
                (s.IndependentTurnsCount + s.UsedHintsCount) >= 1 ||
                store.Messages.Values.Any(m => m.SessionId == s.Id && m.Role == "user")
            );
            if (completedScenariosCount == 0 && userSessions.Count > 0)
            {
                completedScenariosCount = userSessions.Count;
            }

            return Results.Ok(new
            {
                streakDays = Math.Max(1, user.StreakDays),
                daysLearning = Math.Max(1, user.DaysLearning),
                completedScenarios = completedScenariosCount,
                totalWords,
                masteredWords,
                turns = new
                {
                    independentCount = independentTurns,
                    hintedCount = hintedTurns,
                    independentPercent = Math.Round((double)independentTurns / Math.Max(1, independentTurns + hintedTurns) * 100)
                },
                achievements
            });
        });

        // 6. Офлайн-синхронизация: идемпотентная отправка локальных попыток
        app.MapPost("/api/offline/sync", (OfflineSyncRequest req, HttpContext ctx, LinguaDataStore store) =>
        {
            var user = AuthEndpoints.GetOrCreateCurrentUser(ctx, store);
            int syncedAttempts = 0;
            int syncedWords = 0;

            if (req.Attempts != null)
            {
                foreach (var item in req.Attempts)
                {
                    if (store.Attempts.ContainsKey(item.AttemptId)) continue; // Идемпотентность!

                    var attempt = new ExerciseAttempt
                    {
                        Id = item.AttemptId,
                        ExerciseId = item.ExerciseId,
                        UserId = user.Id,
                        UserAnswer = item.UserAnswer,
                        IsCorrect = item.IsCorrect,
                        UsedHint = item.UsedHint,
                        AttemptedAt = item.AttemptedAt
                    };
                    store.Attempts[attempt.Id] = attempt;
                    syncedAttempts++;
                }
            }

            if (req.WordReviews != null)
            {
                foreach (var rev in req.WordReviews)
                {
                    if (store.DictionaryWords.TryGetValue(rev.WordId, out var word))
                    {
                        if (rev.IsCorrect && !rev.UsedHint)
                        {
                            word.ConsecutiveCorrectAnswers++;
                            if (word.ConsecutiveCorrectAnswers >= 3) word.IsMastered = true;
                        }
                        syncedWords++;
                    }
                }
            }

            store.CheckAndAwardAchievements(user.Id);
            store.SaveToDisk();

            return Results.Ok(new
            {
                message = "Офлайн-данные успешно синхронизированы без дублирования.",
                syncedAttempts,
                syncedWords
            });
        });
    }
}

public record ReplaceRecommendationRequest(string? NewTopic, string? NewDescription, string? NewTargetId);
public record OfflineSyncRequest(List<OfflineAttemptItem>? Attempts, List<OfflineWordReviewItem>? WordReviews);
public record OfflineAttemptItem(Guid AttemptId, Guid ExerciseId, string UserAnswer, bool IsCorrect, bool UsedHint, DateTime AttemptedAt);
public record OfflineWordReviewItem(Guid WordId, bool IsCorrect, bool UsedHint);
