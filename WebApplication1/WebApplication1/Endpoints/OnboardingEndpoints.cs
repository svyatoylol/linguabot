using LinguaBot.Models;
using LinguaBot.Services;

namespace LinguaBot.Endpoints;

public static class OnboardingEndpoints
{
    public static void MapOnboardingEndpoints(this WebApplication app)
    {
        // Создание гостевой обучающей сессии (контракт POST /api/guest/sessions)
        app.MapPost("/api/guest/sessions", (CreateGuestSessionRequest req, HttpContext ctx, LinguaDataStore store, ScenarioCatalog catalog) =>
        {
            var user = AuthEndpoints.GetOrCreateCurrentUser(ctx, store);

            if (!string.IsNullOrWhiteSpace(req.TargetLanguage))
                user.Settings.LearningLanguage = req.TargetLanguage.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(req.Goal))
                user.Role = req.Goal.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(req.Level))
                user.Settings.Level = req.Level.Trim();

            var scenario = catalog.GetById(req.ScenarioId ?? "introduction") ?? catalog.GetAll().First();

            var session = new DialogueSession
            {
                UserId = user.Id,
                Scenario = scenario.Id,
                ScenarioTitle = scenario.Title,
                LanguageCode = user.Settings.LearningLanguage,
                Level = user.Settings.Level,
                Status = "Active",
                StartedAt = DateTime.UtcNow
            };

            // Стартовая реплика бота
            var welcomeMessage = new DialogueMessage
            {
                SessionId = session.Id,
                Role = "assistant",
                Content = GetWelcomeGreeting(scenario.Id, session.LanguageCode)
            };

            session.Messages.Add(welcomeMessage);
            store.Sessions[session.Id] = session;
            store.Messages[welcomeMessage.Id] = welcomeMessage;
            store.SaveToDisk();

            return Results.Ok(new
            {
                message = "Гостевой диалог создан.",
                sessionId = session.Id,
                dialogueId = session.Id,
                scenario = new
                {
                    scenario.Id,
                    scenario.Title,
                    scenario.Description,
                    scenario.BotRole,
                    scenario.UserGoal
                },
                welcomeMessage = welcomeMessage.Content
            });
        });

        // Сохранение промежуточного ответа теста (контракт POST /api/onboarding/assessment/answer)
        app.MapPost("/api/onboarding/assessment/answer", (AssessmentAnswerRequest req, HttpContext ctx, LinguaDataStore store) =>
        {
            var user = AuthEndpoints.GetOrCreateCurrentUser(ctx, store);
            if (!string.IsNullOrWhiteSpace(req.TargetLanguage))
                user.Settings.LearningLanguage = req.TargetLanguage.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(req.Goal))
                user.Role = req.Goal.Trim().ToLowerInvariant();

            store.SaveToDisk();
            return Results.Ok(new { message = "Ответ сохранён.", step = req.Step });
        });

        // Расчет и завершение вводного теста (контракт POST /api/onboarding/assessment/complete)
        app.MapPost("/api/onboarding/assessment/complete", (AssessmentCompleteRequest req, HttpContext ctx, LinguaDataStore store) =>
        {
            var user = AuthEndpoints.GetOrCreateCurrentUser(ctx, store);

            if (!string.IsNullOrWhiteSpace(req.TargetLanguage))
                user.Settings.LearningLanguage = req.TargetLanguage.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(req.Goal))
                user.Role = req.Goal.Trim().ToLowerInvariant();

            // Оценка по ответам (beginner -> A1-A2, basic -> B1-B2, confident -> C1)
            int confidentCount = req.Answers.Count(a => a.Contains("confident", StringComparison.OrdinalIgnoreCase) || a.Contains("advanced", StringComparison.OrdinalIgnoreCase));
            int basicCount = req.Answers.Count(a => a.Contains("basic", StringComparison.OrdinalIgnoreCase) || a.Contains("intermediate", StringComparison.OrdinalIgnoreCase));

            string calculatedLevel = "A1-A2";
            if (confidentCount >= 2) calculatedLevel = "C1";
            else if (confidentCount >= 1 || basicCount >= 2) calculatedLevel = "B1-B2";

            user.Settings.Level = calculatedLevel;

            var profile = user.LanguageProfiles.FirstOrDefault(p => p.LanguageCode == user.Settings.LearningLanguage);
            if (profile != null)
            {
                profile.SelectedLevel = calculatedLevel;
                profile.CurrentLevel = calculatedLevel;
                profile.IsPlacementCompleted = true;
                profile.LastTestedAt = DateTime.UtcNow;
            }

            store.SaveToDisk();

            return Results.Ok(new
            {
                level = calculatedLevel,
                message = $"Ваш ориентировочный уровень: {calculatedLevel}. Мы настроили подходящую сложность диалогов."
            });
        });

        // Отложить тест (контракт POST /api/onboarding/skip)
        app.MapPost("/api/onboarding/skip", (SkipAssessmentRequest req, HttpContext ctx, LinguaDataStore store) =>
        {
            var user = AuthEndpoints.GetOrCreateCurrentUser(ctx, store);
            if (!string.IsNullOrWhiteSpace(req.TargetLanguage))
                user.Settings.LearningLanguage = req.TargetLanguage.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(req.Goal))
                user.Role = req.Goal.Trim().ToLowerInvariant();

            store.SaveToDisk();
            return Results.Ok(new { message = "Тест отложен. Уровень можно изменить в любое время в профиле." });
        });
    }

    private static string GetWelcomeGreeting(string scenarioId, string lang)
    {
        if (lang == "es")
        {
            return scenarioId switch
            {
                "cafe" => "¡Hola! Bienvenido a nuestra cafetería. ¿Qué le gustaría tomar hoy?",
                "hotel" => "¡Buenas tardes! Bienvenido al hotel. ¿Tiene una reserva con nosotros?",
                _ => "¡Hola! Mucho gusto en saludarte. ¿Cómo te llamas y de dónde eres?"
            };
        }
        if (lang == "ru")
        {
            return scenarioId switch
            {
                "cafe" => "Здравствуйте! Добро пожаловать. Что для вас приготовить сегодня?",
                "hotel" => "Добрый день! Рады приветствовать вас в нашем отеле. Вы бронировали номер?",
                _ => "Привет! Рад познакомиться. Как вас зовут и чем вы любите заниматься?"
            };
        }

        return scenarioId switch
        {
            "cafe" => "Hi there! Welcome to Central Perk café. What can I get started for you today?",
            "shop" or "shopping" => "Hello! Are you looking for anything in particular today, or just browsing our new arrivals?",
            "directions" => "Excuse me! You look like you might need some help finding your way. Where are you heading?",
            "transport" => "Next passenger, please! Where would you like to travel today?",
            "hotel" => "Good afternoon and welcome to Grand Hotel! Are you checking in today?",
            "trip" or "trip_planning" => "Hey! I'm so excited about our vacation. Where should we plan to visit first?",
            "interview" or "job_interview" => "Good morning, thank you for joining us today. To start our conversation, could you briefly introduce yourself?",
            "business-meeting" or "business_meeting" => "Good morning team! Thanks for joining the sprint alignment. Let's start with a quick update on current tasks.",
            "film" => "Hey! I just saw that new movie everyone's discussing. Have you seen anything interesting recently?",
            _ => "Hello! Welcome to LinguaBot practice. Could you tell me a little bit about yourself?"
        };
    }
}

public record CreateGuestSessionRequest(string? ScenarioId, string? TargetLanguage, string? Goal, string? Level);
public record AssessmentAnswerRequest(string QuestionId, string Answer, int Step, string? TargetLanguage, string? Goal);
public record AssessmentCompleteRequest(List<string> Answers, string? TargetLanguage, string? Goal);
public record SkipAssessmentRequest(string? TargetLanguage, string? Goal);
