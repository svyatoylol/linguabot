using System.Text.Json;
using LinguaBot.Models;
using LinguaBot.Services;

namespace LinguaBot.Endpoints;

public static class PlacementTestEndpoints
{
    public static void MapPlacementTestEndpoints(this WebApplication app)
    {
        // ==========================================
        // 1. CREATE OR RESUME PLACEMENT TEST
        // ==========================================
        app.MapPost("/api/placement-tests", async (
            CreatePlacementTestRequest? request,
            HttpContext context,
            LinguaDataStore store,
            PlacementTestGeneratorService generator,
            IntelligentLanguageEngine engine,
            CancellationToken cancellationToken) =>
        {
            var user = AuthEndpoints.GetOrCreateCurrentUser(context, store);
            string lang = !string.IsNullOrWhiteSpace(request?.LanguageCode)
                ? request.LanguageCode.Trim().ToLowerInvariant()
                : user.Settings.LearningLanguage;

            // Возвращаем незавершённый тест, если он уже запущен
            var existingTest = store.PlacementTests.Values
                .Where(t => t.UserId == user.Id && t.Status == "InProgress")
                .OrderByDescending(t => t.StartedAt)
                .FirstOrDefault();

            if (existingTest != null && existingTest.Questions.Count > 0)
            {
                return Results.Ok(BuildTestResponse(existingTest));
            }

            var generatedQuestions = await generator.GenerateAsync(lang, user.Settings.Level, cancellationToken);
            if (generatedQuestions == null || generatedQuestions.Count == 0)
            {
                var fallbackQuestions = engine.GeneratePlacementQuestions(user.Id, lang);
                generatedQuestions = fallbackQuestions.Select(q =>
                {
                    List<string> options = new();
                    try { options = JsonSerializer.Deserialize<List<string>>(q.Options) ?? new(); } catch { }
                    return new GeneratedPlacementQuestion
                    {
                        Skill = q.Skill,
                        Type = q.Type,
                        Prompt = q.Prompt,
                        Options = options,
                        CorrectAnswer = q.CorrectAnswer,
                        Explanation = q.Explanation
                    };
                }).ToList();
            }

            var profile = user.LanguageProfiles.FirstOrDefault(p => p.LanguageCode == lang)
                ?? user.LanguageProfiles.FirstOrDefault();

            var test = new PlacementTestSession
            {
                UserId = user.Id,
                LanguageProfileId = profile?.Id ?? Guid.NewGuid(),
                Status = "InProgress",
                TotalQuestions = generatedQuestions.Count,
                CorrectAnswers = 0,
                StartedAt = DateTime.UtcNow
            };

            int order = 1;
            foreach (var g in generatedQuestions)
            {
                test.Questions.Add(new PlacementTestQuestion
                {
                    PlacementTestSessionId = test.Id,
                    Order = order++,
                    Skill = g.Skill,
                    Type = g.Type,
                    Prompt = g.Prompt,
                    Options = JsonSerializer.Serialize(g.Options),
                    CorrectAnswer = g.CorrectAnswer,
                    Explanation = g.Explanation
                });
            }

            store.PlacementTests[test.Id] = test;
            user.PlacementTests.Add(test);
            store.SaveToDisk();

            return Results.Ok(BuildTestResponse(test));
        });

        // ==========================================
        // 2. GET TEST STATUS / RESUME
        // ==========================================
        app.MapGet("/api/placement-tests/{testId:guid}", (
            Guid testId,
            HttpContext context,
            LinguaDataStore store) =>
        {
            if (!store.PlacementTests.TryGetValue(testId, out var test))
            {
                return Results.NotFound(new { error = "Тест не найден." });
            }

            return Results.Ok(BuildTestResponse(test));
        });

        // ==========================================
        // 3. SUBMIT ANSWER
        // ==========================================
        app.MapPost("/api/placement-tests/{testId:guid}/answers", (
            Guid testId,
            SubmitPlacementAnswerRequest request,
            HttpContext context,
            LinguaDataStore store) =>
        {
            if (!store.PlacementTests.TryGetValue(testId, out var test))
            {
                return Results.NotFound(new { error = "Тест не найден." });
            }

            if (test.Status != "InProgress")
            {
                return Results.Conflict(new { error = "Этот тест уже завершён." });
            }

            var question = test.Questions.FirstOrDefault(q => q.Id == request.QuestionId);
            if (question == null)
            {
                return Results.NotFound(new { error = "Вопрос не найден." });
            }

            if (test.Answers.Any(a => a.PlacementTestQuestionId == question.Id))
            {
                return Results.Conflict(new { error = "На этот вопрос уже дан ответ." });
            }

            bool isCorrect = Normalize(question.CorrectAnswer) == Normalize(request.Answer);

            var answer = new PlacementTestAnswer
            {
                PlacementTestSessionId = test.Id,
                PlacementTestQuestionId = question.Id,
                UserAnswer = request.Answer.Trim(),
                IsCorrect = isCorrect,
                AnsweredAt = DateTime.UtcNow
            };

            test.Answers.Add(answer);
            if (isCorrect) test.CorrectAnswers++;

            store.SaveToDisk();

            int answeredCount = test.Answers.Select(a => a.PlacementTestQuestionId).Distinct().Count();

            return Results.Ok(new
            {
                message = "Ответ сохранён.",
                isCorrect,
                correctAnswer = question.CorrectAnswer,
                explanation = question.Explanation,
                answeredCount,
                totalQuestions = test.TotalQuestions,
                isComplete = answeredCount >= test.TotalQuestions
            });
        });

        // ==========================================
        // 4. FINISH TEST & CALCULATE CEFR LEVEL
        // ==========================================
        app.MapPost("/api/placement-tests/{testId:guid}/finish", (
            Guid testId,
            HttpContext context,
            LinguaDataStore store) =>
        {
            if (!store.PlacementTests.TryGetValue(testId, out var test))
            {
                return Results.NotFound(new { error = "Тест не найден." });
            }

            var user = AuthEndpoints.GetOrCreateCurrentUser(context, store);

            if (test.Status == "Completed")
            {
                return Results.Ok(BuildResultResponse(test, user));
            }

            int correctAnswers = test.Answers.Count(a => a.IsCorrect);
            test.CorrectAnswers = correctAnswers;

            var basicQuestions = test.Questions
                .Where(q => q.Order >= 1 && q.Order <= 4)
                .Select(q => q.Id)
                .ToHashSet();

            var intermediateQuestions = test.Questions
                .Where(q => q.Order >= 5 && q.Order <= 8)
                .Select(q => q.Id)
                .ToHashSet();

            var advancedQuestions = test.Questions
                .Where(q => q.Order >= 9 && q.Order <= 12)
                .Select(q => q.Id)
                .ToHashSet();

            int basicCorrect = test.Answers.Count(a => a.IsCorrect && basicQuestions.Contains(a.PlacementTestQuestionId));
            int intermediateCorrect = test.Answers.Count(a => a.IsCorrect && intermediateQuestions.Contains(a.PlacementTestQuestionId));
            int advancedCorrect = test.Answers.Count(a => a.IsCorrect && advancedQuestions.Contains(a.PlacementTestQuestionId));

            string resultLevel;
            if (basicCorrect < 3)
            {
                resultLevel = "A1-A2";
            }
            else if (intermediateCorrect < 3)
            {
                resultLevel = "B1-B2";
            }
            else if (advancedCorrect >= 2)
            {
                resultLevel = "C1";
            }
            else
            {
                resultLevel = "B1-B2";
            }

            test.ResultLevel = resultLevel;
            test.Status = "Completed";
            test.FinishedAt = DateTime.UtcNow;

            // Обновляем текущий уровень пользователя и его профиль
            user.Settings.Level = resultLevel;
            var profile = user.LanguageProfiles.FirstOrDefault(p => p.Id == test.LanguageProfileId)
                ?? user.LanguageProfiles.FirstOrDefault(p => p.LanguageCode == user.Settings.LearningLanguage);

            if (profile != null)
            {
                profile.CurrentLevel = resultLevel;
                profile.SelectedLevel = resultLevel;
                profile.IsPlacementCompleted = true;
                profile.LastTestedAt = DateTime.UtcNow;
                if (resultLevel == "C1") { profile.GrammarScore = 88; profile.SpeakingScore = 82; profile.VocabScore = 90; }
                else if (resultLevel == "B1-B2") { profile.GrammarScore = 70; profile.SpeakingScore = 65; profile.VocabScore = 72; }
                else { profile.GrammarScore = 45; profile.SpeakingScore = 40; profile.VocabScore = 48; }
            }

            store.CheckAndAwardAchievements(user.Id);
            store.SaveToDisk();

            return Results.Ok(BuildResultResponse(test, user, basicCorrect, intermediateCorrect, advancedCorrect));
        });

        // ==========================================
        // 5. GET TEST RESULT
        // ==========================================
        app.MapGet("/api/placement-tests/{testId:guid}/result", (
            Guid testId,
            HttpContext context,
            LinguaDataStore store) =>
        {
            if (!store.PlacementTests.TryGetValue(testId, out var test))
            {
                return Results.NotFound(new { error = "Тест не найден." });
            }

            var user = AuthEndpoints.GetOrCreateCurrentUser(context, store);
            return Results.Ok(BuildResultResponse(test, user));
        });
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        return string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim().ToLowerInvariant();
    }

    private static object BuildTestResponse(PlacementTestSession test)
    {
        return new
        {
            test.Id,
            test.Status,
            test.StartedAt,
            test.TotalQuestions,
            AnsweredCount = test.Answers.Select(a => a.PlacementTestQuestionId).Distinct().Count(),
            Questions = test.Questions
                .OrderBy(q => q.Order)
                .Select(q =>
                {
                    var answer = test.Answers.FirstOrDefault(a => a.PlacementTestQuestionId == q.Id);
                    List<string> options = new();
                    try
                    {
                        options = JsonSerializer.Deserialize<List<string>>(q.Options) ?? new();
                    }
                    catch { }

                    return new
                    {
                        q.Id,
                        q.Order,
                        q.Skill,
                        q.Type,
                        q.Prompt,
                        Options = options,
                        IsAnswered = answer != null,
                        UserAnswer = answer?.UserAnswer
                    };
                })
        };
    }

    private static object BuildResultResponse(
        PlacementTestSession test,
        User user,
        int? basic = null,
        int? intermediate = null,
        int? advanced = null)
    {
        int total = Math.Max(1, test.TotalQuestions);
        int correct = test.CorrectAnswers;

        return new
        {
            test.Id,
            test.Status,
            LanguageCode = user.Settings.LearningLanguage,
            CurrentLevel = test.ResultLevel ?? user.Settings.Level,
            test.TotalQuestions,
            test.CorrectAnswers,
            BasicCorrect = basic ?? Math.Min(4, correct),
            IntermediateCorrect = intermediate ?? Math.Max(0, Math.Min(4, correct - 4)),
            AdvancedCorrect = advanced ?? Math.Max(0, correct - 8),
            Score = Math.Round((double)correct / total * 100),
            Recommendation = $"Ваш уровень владения языком определён как {test.ResultLevel ?? user.Settings.Level}. Рекомендуем начать со сценариев повседневного общения и оттачивать спонтанную речь в режиме «Без заготовки»."
        };
    }
}

public record CreatePlacementTestRequest(string? LanguageCode = null);

public record SubmitPlacementAnswerRequest(
    Guid QuestionId,
    string Answer
);
