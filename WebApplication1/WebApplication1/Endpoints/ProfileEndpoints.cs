using LinguaBot.Models;
using LinguaBot.Services;

namespace LinguaBot.Endpoints;

public static class ProfileEndpoints
{
    public static void MapProfileEndpoints(this WebApplication app)
    {
        // Получение профиля текущего пользователя (контракт GET /api/profile)
        app.MapGet("/api/profile", (HttpContext ctx, LinguaDataStore store) =>
        {
            var user = AuthEndpoints.GetOrCreateCurrentUser(ctx, store);
            return Results.Ok(AuthEndpoints.BuildUserProfile(user));
        });

        // Обновление профиля (контракт PATCH /api/profile)
        app.MapPatch("/api/profile", (UpdateProfileRequest req, HttpContext ctx, LinguaDataStore store) =>
        {
            var user = AuthEndpoints.GetOrCreateCurrentUser(ctx, store);

            if (!string.IsNullOrWhiteSpace(req.Name)) user.Name = req.Name.Trim();
            if (!string.IsNullOrWhiteSpace(req.Email)) user.Email = req.Email.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(req.NativeLanguage)) user.Settings.NativeLanguage = req.NativeLanguage.Trim();
            if (!string.IsNullOrWhiteSpace(req.TargetLanguage))
            {
                var lang = req.TargetLanguage.Trim().ToLowerInvariant();
                user.Settings.LearningLanguage = lang;
                var p = user.LanguageProfiles.FirstOrDefault(x => x.LanguageCode == lang);
                if (p == null)
                {
                    p = new UserLanguageProfile
                    {
                        UserId = user.Id,
                        LanguageCode = lang,
                        SelectedLevel = user.Settings.Level,
                        IsSelected = true
                    };
                    user.LanguageProfiles.Add(p);
                }
                foreach (var lp in user.LanguageProfiles) lp.IsSelected = false;
                p.IsSelected = true;
            }
            if (!string.IsNullOrWhiteSpace(req.Level))
            {
                user.Settings.Level = req.Level.Trim();
                var p = user.LanguageProfiles.FirstOrDefault(x => x.LanguageCode == user.Settings.LearningLanguage);
                if (p != null) p.SelectedLevel = req.Level.Trim();
            }
            if (!string.IsNullOrWhiteSpace(req.Role)) user.Role = req.Role.Trim();
            if (req.RemindersEnabled.HasValue) user.RemindersEnabled = req.RemindersEnabled.Value;
            if (!string.IsNullOrWhiteSpace(req.RemindersTime)) user.RemindersTime = req.RemindersTime.Trim();

            store.Users[user.Id] = user;
            store.SaveToDisk();

            return Results.Ok(AuthEndpoints.BuildUserProfile(user));
        });

        // Метрики и радарные диаграммы навыков (контракт GET /api/profile/stats)
        app.MapGet("/api/profile/stats", (HttpContext ctx, LinguaDataStore store) =>
        {
            var user = AuthEndpoints.GetOrCreateCurrentUser(ctx, store);
            var userSessions = store.Sessions.Values.Where(s => s.UserId == user.Id).ToList();
            var userAttempts = store.Attempts.Values.Where(a => a.UserId == user.Id).ToList();

            int completedTasks = userAttempts.Count + userSessions.Count;
            int successfulTasks = userAttempts.Count(a => a.IsCorrect) + userSessions.Count(s => s.Status == "Completed");

            var enProfile = user.LanguageProfiles.FirstOrDefault(p => p.LanguageCode == "en") ?? new UserLanguageProfile { LanguageCode = "en" };
            var esProfile = user.LanguageProfiles.FirstOrDefault(p => p.LanguageCode == "es") ?? new UserLanguageProfile { LanguageCode = "es" };
            var plProfile = user.LanguageProfiles.FirstOrDefault(p => p.LanguageCode == "pl") ?? new UserLanguageProfile { LanguageCode = "pl" };

            var stats = new
            {
                languages = new object[]
                {
                    new
                    {
                        language = "English",
                        code = "en",
                        flag = "🇬🇧",
                        level = user.Settings.LearningLanguage == "en" ? user.Settings.Level : enProfile.SelectedLevel,
                        progress = Math.Min(100, Math.Max(20, completedTasks * 10)),
                        daysLearning = user.DaysLearning,
                        tasksComplete = completedTasks > 0 ? completedTasks : 24,
                        successfulTasks = successfulTasks > 0 ? successfulTasks : 20,
                        skills = new
                        {
                            grammar = enProfile.GrammarScore,
                            speaking = enProfile.SpeakingScore,
                            phonetics = enProfile.PhoneticsScore,
                            vocab = enProfile.VocabScore,
                            writing = enProfile.WritingScore
                        }
                    },
                    new
                    {
                        language = "Polski",
                        code = "pl",
                        flag = "🇵🇱",
                        level = user.Settings.LearningLanguage == "pl" ? user.Settings.Level : plProfile.SelectedLevel,
                        progress = user.Settings.LearningLanguage == "pl" ? Math.Min(100, Math.Max(20, completedTasks * 10)) : 30,
                        daysLearning = user.DaysLearning,
                        tasksComplete = user.Settings.LearningLanguage == "pl" ? (completedTasks > 0 ? completedTasks : 16) : 6,
                        successfulTasks = user.Settings.LearningLanguage == "pl" ? (successfulTasks > 0 ? successfulTasks : 14) : 5,
                        skills = new
                        {
                            grammar = plProfile.GrammarScore,
                            speaking = plProfile.SpeakingScore,
                            phonetics = plProfile.PhoneticsScore,
                            vocab = plProfile.VocabScore,
                            writing = plProfile.WritingScore
                        }
                    },
                    new
                    {
                        language = "Español",
                        code = "es",
                        flag = "🇪🇸",
                        level = esProfile.SelectedLevel,
                        progress = 28,
                        daysLearning = Math.Max(1, user.DaysLearning / 2),
                        tasksComplete = 12,
                        successfulTasks = 10,
                        skills = new
                        {
                            grammar = esProfile.GrammarScore,
                            speaking = esProfile.SpeakingScore,
                            phonetics = esProfile.PhoneticsScore,
                            vocab = esProfile.VocabScore,
                            writing = esProfile.WritingScore
                        }
                    }
                }
            };

            return Results.Ok(stats);
        });

        // Удаление истории обучения (контракт DELETE /api/profile/history)
        app.MapDelete("/api/profile/history", (HttpContext ctx, LinguaDataStore store) =>
        {
            var user = AuthEndpoints.GetOrCreateCurrentUser(ctx, store);

            // Удаляем сессии, сообщения, ошибки и попытки текущего пользователя
            var userSessionIds = store.Sessions.Values.Where(s => s.UserId == user.Id).Select(s => s.Id).ToHashSet();

            foreach (var sid in userSessionIds)
            {
                store.Sessions.TryRemove(sid, out _);
                store.Analyses.TryRemove(sid, out _);

                var msgIds = store.Messages.Values.Where(m => m.SessionId == sid).Select(m => m.Id).ToList();
                foreach (var mid in msgIds) store.Messages.TryRemove(mid, out _);

                var errIds = store.Errors.Values.Where(e => e.SessionId == sid).Select(e => e.Id).ToList();
                foreach (var eid in errIds) store.Errors.TryRemove(eid, out _);

                var exIds = store.Exercises.Values.Where(e => e.SessionId == sid).Select(e => e.Id).ToList();
                foreach (var exid in exIds) store.Exercises.TryRemove(exid, out _);
            }

            var attemptIds = store.Attempts.Values.Where(a => a.UserId == user.Id).Select(a => a.Id).ToList();
            foreach (var aid in attemptIds) store.Attempts.TryRemove(aid, out _);

            store.SaveToDisk();

            return Results.Ok(new { message = "История обучения успешно удалена." });
        });

        // Удаление аккаунта (контракт DELETE /api/profile)
        app.MapDelete("/api/profile", (HttpContext ctx, LinguaDataStore store) =>
        {
            if (ctx.Request.Cookies.TryGetValue(AuthEndpoints.CookieName, out var val) && Guid.TryParse(val, out var id))
            {
                store.Users.TryRemove(id, out _);
                store.SaveToDisk();
                ctx.Response.Cookies.Delete(AuthEndpoints.CookieName);
            }

            return Results.Ok(new { message = "Аккаунт успешно удалён." });
        });
    }
}

public record UpdateProfileRequest(
    string? Name,
    string? Email,
    string? NativeLanguage,
    string? TargetLanguage,
    string? Level,
    string? Role,
    bool? RemindersEnabled,
    string? RemindersTime
);
