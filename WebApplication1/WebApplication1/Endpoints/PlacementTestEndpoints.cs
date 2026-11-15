
using System.Text.Json;
using LinguaBot.Data;
using LinguaBot.Models;
using LinguaBot.Services;
using Microsoft.EntityFrameworkCore;

namespace LinguaBot.Endpoints;

public static class PlacementTestEndpoints
{
    public static void MapPlacementTestEndpoints(this WebApplication app)
    {
        // ==========================================
        // CREATE OR RESUME TEST
        // ==========================================

        app.MapPost("/api/placement-tests", async (
            CreatePlacementTestRequest request,
            HttpContext context,
            ApplicationDbContext db,
            PlacementTestGeneratorService generator,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetGuestId(context, out var userId))
                return Results.Unauthorized();

            string languageCode =
                request.LanguageCode?.Trim().ToLowerInvariant() ?? "";

            var profile = await db.UserLanguageProfiles
                .FirstOrDefaultAsync(
                    p => p.UserId == userId &&
                         p.LanguageCode == languageCode &&
                         p.IsSelected,
                    cancellationToken
                );

            if (profile == null)
            {
                return Results.NotFound(new
                {
                    error = "Selected language profile was not found."
                });
            }

            // Возвращаем незавершённый тест, если он существует.
            var existingTest = await db.PlacementTestSessions
                .Include(t => t.Questions)
                .Include(t => t.Answers)
                .Where(t =>
                    t.UserId == userId &&
                    t.LanguageProfileId == profile.Id &&
                    t.Status == "InProgress")
                .OrderByDescending(t => t.StartedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingTest != null)
                return Results.Ok(BuildTestResponse(existingTest));

            try
            {
                var generatedQuestions = await generator.GenerateAsync(
                    profile.LanguageCode,
                    profile.SelectedLevel,
                    cancellationToken
                );

                var test = new PlacementTestSession
                {
                    UserId = userId,
                    LanguageProfileId = profile.Id,
                    Status = "InProgress",
                    TotalQuestions = generatedQuestions.Count,
                    CorrectAnswers = 0,
                    StartedAt = DateTime.UtcNow
                };

                foreach (var generated in generatedQuestions)
                {
                    test.Questions.Add(new PlacementTestQuestion
                    {
                        Order = test.Questions.Count + 1,
                        Skill = generated.Skill,
                        Type = generated.Type,
                        Prompt = generated.Prompt,
                        Options = JsonSerializer.Serialize(
                            generated.Options
                        ),
                        CorrectAnswer = generated.CorrectAnswer,
                        Explanation = generated.Explanation
                    });
                }

                db.PlacementTestSessions.Add(test);

                await db.SaveChangesAsync(cancellationToken);

                return Results.Ok(BuildTestResponse(test));
            }
            catch (HttpRequestException)
            {
                return Results.Problem(
                    title: "AI service unavailable",
                    detail: "Could not generate the placement test.",
                    statusCode: 502
                );
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(
                    title: "Test generation failed",
                    detail: ex.Message,
                    statusCode: 500
                );
            }
            catch (JsonException)
            {
                return Results.Problem(
                    title: "Invalid AI response",
                    detail: "Could not process generated questions.",
                    statusCode: 500
                );
            }
        });

        // ==========================================
        // GET TEST / RESUME
        // ==========================================

        app.MapGet("/api/placement-tests/{testId:guid}", async (
            Guid testId,
            HttpContext context,
            ApplicationDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetGuestId(context, out var userId))
                return Results.Unauthorized();

            var test = await db.PlacementTestSessions
                .Include(t => t.Questions)
                .Include(t => t.Answers)
                .FirstOrDefaultAsync(
                    t => t.Id == testId && t.UserId == userId,
                    cancellationToken
                );

            if (test == null)
                return Results.NotFound();

            return Results.Ok(BuildTestResponse(test));
        });

        // ==========================================
        // SUBMIT ANSWER
        // ==========================================

        app.MapPost("/api/placement-tests/{testId:guid}/answers", async (
            Guid testId,
            SubmitPlacementAnswerRequest request,
            HttpContext context,
            ApplicationDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetGuestId(context, out var userId))
                return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Answer))
            {
                return Results.BadRequest(new
                {
                    error = "Answer cannot be empty."
                });
            }

            var test = await db.PlacementTestSessions
                .Include(t => t.Questions)
                .Include(t => t.Answers)
                .FirstOrDefaultAsync(
                    t => t.Id == testId && t.UserId == userId,
                    cancellationToken
                );

            if (test == null)
                return Results.NotFound();

            if (test.Status != "InProgress")
            {
                return Results.Conflict(new
                {
                    error = "This test is no longer active."
                });
            }

            var question = test.Questions.FirstOrDefault(
                q => q.Id == request.QuestionId
            );

            if (question == null)
            {
                return Results.NotFound(new
                {
                    error = "Question was not found."
                });
            }

            bool alreadyAnswered = test.Answers.Any(
                a => a.PlacementTestQuestionId == question.Id
            );

            if (alreadyAnswered)
            {
                return Results.Conflict(new
                {
                    error = "This question has already been answered."
                });
            }

            var options = JsonSerializer.Deserialize<List<string>>(
                question.Options
            ) ?? new List<string>();

            bool validOption = options.Any(
                option => Normalize(option) == Normalize(request.Answer)
            );

            if (!validOption)
            {
                return Results.BadRequest(new
                {
                    error = "The submitted answer is not a valid option."
                });
            }

            bool isCorrect = Normalize(question.CorrectAnswer) ==
                             Normalize(request.Answer);

            var answer = new PlacementTestAnswer
            {
                PlacementTestSessionId = test.Id,
                PlacementTestQuestionId = question.Id,
                UserAnswer = request.Answer.Trim(),
                IsCorrect = isCorrect,
                AnsweredAt = DateTime.UtcNow
            };

            db.PlacementTestAnswers.Add(answer);

            await db.SaveChangesAsync(cancellationToken);

            int answeredCount = test.Answers
                .Select(a => a.PlacementTestQuestionId)
                .Distinct()
                .Count();

            return Results.Ok(new
            {
                message = "Answer saved.",
                answeredCount,
                totalQuestions = test.TotalQuestions,
                isComplete = answeredCount == test.TotalQuestions
            });
        });

        // ==========================================
        // FINISH TEST
        // ==========================================

        app.MapPost("/api/placement-tests/{testId:guid}/finish", async (
            Guid testId,
            HttpContext context,
            ApplicationDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!TryGetGuestId(context, out var userId))
                return Results.Unauthorized();

            var test = await db.PlacementTestSessions
                .Include(t => t.Questions)
                .Include(t => t.Answers)
                .Include(t => t.LanguageProfile)
                .FirstOrDefaultAsync(
                    t => t.Id == testId && t.UserId == userId,
                    cancellationToken
                );

            if (test == null)
                return Results.NotFound();

            // Повторный запрос возвращает уже сохранённый результат.
            if (test.Status == "Completed")
            {
                return Results.Ok(new
                {
                    test.Id,
                    test.Status,
                    test.ResultLevel,
                    test.TotalQuestions,
                    test.CorrectAnswers,
                    Score = test.TotalQuestions == 0
                        ? 0
                        : Math.Round(
                            (double)test.CorrectAnswers /
                            test.TotalQuestions * 100
                        )
                });
            }

            if (test.Status != "InProgress")
            {
                return Results.Conflict(new
                {
                    error = "This test cannot be completed."
                });
            }

            int answeredCount = test.Answers
                .Select(a => a.PlacementTestQuestionId)
                .Distinct()
                .Count();

            if (answeredCount != test.TotalQuestions)
            {
                return Results.BadRequest(new
                {
                    error = "Answer all questions before finishing the test.",
                    answeredCount,
                    totalQuestions = test.TotalQuestions
                });
            }

            int correctAnswers = test.Answers.Count(a => a.IsCorrect);

            // Вопросы 1-4: A1-A2
            // Вопросы 5-8: B1-B2
            // Вопросы 9-12: C1

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

            int basicCorrect = test.Answers.Count(
                a => a.IsCorrect &&
                     basicQuestions.Contains(a.PlacementTestQuestionId)
            );

            int intermediateCorrect = test.Answers.Count(
                a => a.IsCorrect &&
                     intermediateQuestions.Contains(a.PlacementTestQuestionId)
            );

            int advancedCorrect = test.Answers.Count(
                a => a.IsCorrect &&
                     advancedQuestions.Contains(a.PlacementTestQuestionId)
            );

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

            test.CorrectAnswers = correctAnswers;
            test.ResultLevel = resultLevel;
            test.Status = "Completed";
            test.FinishedAt = DateTime.UtcNow;

            test.LanguageProfile.CurrentLevel = resultLevel;
            test.LanguageProfile.IsPlacementCompleted = true;
            test.LanguageProfile.LastTestedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(cancellationToken);

            return Results.Ok(new
            {
                test.Id,
                test.Status,
                LanguageCode = test.LanguageProfile.LanguageCode,
                SelectedLevel = test.LanguageProfile.SelectedLevel,
                CurrentLevel = resultLevel,
                test.TotalQuestions,
                test.CorrectAnswers,
                BasicCorrect = basicCorrect,
                IntermediateCorrect = intermediateCorrect,
                AdvancedCorrect = advancedCorrect,
                Score = Math.Round(
                    (double)correctAnswers / test.TotalQuestions * 100
                )
            });
        });
    }

    // ==========================================
    // HELPERS
    // ==========================================

    private static bool TryGetGuestId(
        HttpContext context,
        out Guid userId)
    {
        userId = Guid.Empty;

        return context.Request.Cookies.TryGetValue(
                   "linguabot_guest",
                   out var value
               ) &&
               Guid.TryParse(value, out userId);
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

    private static object BuildTestResponse(
        PlacementTestSession test)
    {
        return new
        {
            test.Id,
            test.Status,
            test.StartedAt,
            test.TotalQuestions,

            AnsweredCount = test.Answers
                .Select(a => a.PlacementTestQuestionId)
                .Distinct()
                .Count(),

            Questions = test.Questions
                .OrderBy(q => q.Order)
                .Select(q =>
                {
                    var answer = test.Answers.FirstOrDefault(
                        a => a.PlacementTestQuestionId == q.Id
                    );

                    return new
                    {
                        q.Id,
                        q.Order,
                        q.Skill,
                        q.Type,
                        q.Prompt,
                        Options = JsonSerializer.Deserialize<List<string>>(
                            q.Options
                        ) ?? new List<string>(),
                        IsAnswered = answer != null,
                        UserAnswer = answer?.UserAnswer
                    };
                })
        };
    }
}

public record CreatePlacementTestRequest(string LanguageCode);

public record SubmitPlacementAnswerRequest(
    Guid QuestionId,
    string Answer
);
