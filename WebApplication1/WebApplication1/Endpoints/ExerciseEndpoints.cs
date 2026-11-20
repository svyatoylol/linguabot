using LinguaBot.Models;
using LinguaBot.Models.Exercises;
using LinguaBot.Services;

namespace LinguaBot.Endpoints;

public static class ExerciseEndpoints
{
    public static void MapExerciseEndpoints(this WebApplication app)
    {
        // Получение адаптивных упражнений для сессии
        app.MapGet("/api/dialogues/{sessionId:guid}/exercises", async (
            Guid sessionId,
            ExerciseGeneratorService generator,
            CancellationToken ct) =>
        {
            var exercises = await generator.GenerateAsync(sessionId, ct);
            return Results.Ok(exercises);
        });

        // Отправка ответа на упражнение с мгновенной проверкой
        app.MapPost("/api/dialogues/{sessionId:guid}/exercises/{exerciseId:guid}/attempts", (
            Guid sessionId,
            Guid exerciseId,
            SubmitExerciseAttemptRequest req,
            HttpContext ctx,
            LinguaDataStore store) =>
        {
            if (string.IsNullOrWhiteSpace(req.Answer))
            {
                return Results.BadRequest(new { message = "Ответ не может быть пустым." });
            }

            if (!store.Exercises.TryGetValue(exerciseId, out var exercise))
            {
                return Results.NotFound(new { message = "Упражнение не найдено." });
            }

            var user = AuthEndpoints.GetOrCreateCurrentUser(ctx, store);

            string normUser = Normalize(req.Answer);
            string normCorrect = Normalize(exercise.CorrectAnswer);

            bool isCorrect = string.Equals(normUser, normCorrect, StringComparison.OrdinalIgnoreCase) ||
                             normCorrect.Contains(normUser, StringComparison.OrdinalIgnoreCase) && normUser.Length > 3;

            var attempt = new ExerciseAttempt
            {
                ExerciseId = exercise.Id,
                UserId = user.Id,
                UserAnswer = req.Answer.Trim(),
                IsCorrect = isCorrect,
                AttemptedAt = DateTime.UtcNow
            };

            store.Attempts[attempt.Id] = attempt;
            exercise.Attempts.Add(attempt);

            int count = store.Attempts.Values.Count(a => a.ExerciseId == exercise.Id);

            store.CheckAndAwardAchievements(user.Id);
            store.SaveToDisk();

            return Results.Ok(new ExerciseAttemptResponse
            {
                Id = attempt.Id,
                IsCorrect = isCorrect,
                AttemptsCount = count,
                CanRetry = !isCorrect,
                Feedback = isCorrect
                    ? $"Отлично! {exercise.Explanation}"
                    : $"Ответ не совсем точный. Подсказка: {exercise.Explanation}",
                AttemptedAt = attempt.AttemptedAt
            });
        });

        // Каталог практических упражнений по категориям (Grammar, Vocabulary, Listening, Offline)
        app.MapGet("/api/exercises/practice", (string? category, string? level, ExerciseGeneratorService generator) =>
        {
            var result = generator.GetPracticeExercises(category, level);
            return Results.Ok(result);
        });
    }

    private static string Normalize(string val)
    {
        return string.Join(" ", (val ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();
    }
}

public record SubmitExerciseAttemptRequest(string Answer);
