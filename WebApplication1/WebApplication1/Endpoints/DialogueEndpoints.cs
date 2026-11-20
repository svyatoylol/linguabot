using LinguaBot.Models;
using LinguaBot.Services;

namespace LinguaBot.Endpoints;

public static class DialogueEndpoints
{
    public static void MapDialogueEndpoints(this WebApplication app)
    {
        // 1. Каталог 10 диалоговых сценариев
        app.MapGet("/api/scenarios", (ScenarioCatalog catalog) =>
        {
            return Results.Ok(catalog.GetAll());
        });

        // 2. Создание диалога по выбранному сценарию (контракт POST /api/dialogues)
        app.MapPost("/api/dialogues", (CreateDialogueSessionRequest req, HttpContext ctx, LinguaDataStore store, ScenarioCatalog catalog) =>
        {
            var user = AuthEndpoints.GetOrCreateCurrentUser(ctx, store);
            var scenario = catalog.GetById(req.ScenarioId ?? "introduction") ?? catalog.GetAll().First();

            string lang = !string.IsNullOrWhiteSpace(req.TargetLanguage)
                ? req.TargetLanguage.Trim().ToLowerInvariant()
                : user.Settings.LearningLanguage;

            // Синхронизируем выбранный язык в профиле пользователя
            user.Settings.LearningLanguage = lang;
            var activeProf = user.LanguageProfiles.FirstOrDefault(p => p.LanguageCode == lang);
            if (activeProf == null)
            {
                activeProf = new UserLanguageProfile
                {
                    UserId = user.Id,
                    LanguageCode = lang,
                    SelectedLevel = user.Settings.Level,
                    IsSelected = true
                };
                user.LanguageProfiles.Add(activeProf);
            }
            foreach (var lp in user.LanguageProfiles) lp.IsSelected = false;
            activeProf.IsSelected = true;

            string level = !string.IsNullOrWhiteSpace(req.Level)
                ? req.Level.Trim()
                : user.Settings.Level;

            string mode = !string.IsNullOrWhiteSpace(req.Mode) ? req.Mode.Trim() : "Standard";

            var session = new DialogueSession
            {
                UserId = user.Id,
                Scenario = scenario.Id,
                ScenarioTitle = scenario.Title,
                LanguageCode = lang,
                Level = level,
                Mode = mode,
                Status = "Active",
                StartedAt = DateTime.UtcNow
            };

            // Стартовая реплика бота
            string greeting = mode == "NoCheatSheet"
                ? "Давайте проверим ваши навыки без заготовок! Попробуйте ответить на мой вопрос своими словами, объяснив причину своими формулировками."
                : OnboardingEndpointsExtension.MapOnboardingEndpointsExtensionGetWelcome(scenario.Id, lang);

            var welcomeMessage = new DialogueMessage
            {
                SessionId = session.Id,
                Role = "assistant",
                Content = greeting
            };

            session.Messages.Add(welcomeMessage);
            store.Sessions[session.Id] = session;
            store.Messages[welcomeMessage.Id] = welcomeMessage;

            user.Sessions.Add(session);
            store.SaveToDisk();

            return Results.Ok(new
            {
                dialogueId = session.Id,
                sessionId = session.Id,
                mode = session.Mode,
                scenario = new
                {
                    scenario.Id,
                    scenario.Title,
                    scenario.Description,
                    scenario.BotRole,
                    scenario.UserGoal,
                    scenario.Category,
                    scenario.Tag,
                    scenario.Icon
                },
                welcomeMessage = welcomeMessage.Content,
                messages = new object[]
                {
                    new
                    {
                        welcomeMessage.Id,
                        welcomeMessage.Role,
                        welcomeMessage.Content,
                        welcomeMessage.CreatedAt
                    }
                }
            });
        });

        // 3. Получение состояния диалога
        app.MapGet("/api/dialogues/{sessionId:guid}", (Guid sessionId, HttpContext ctx, LinguaDataStore store, ScenarioCatalog catalog) =>
        {
            if (!store.Sessions.TryGetValue(sessionId, out var session))
            {
                return Results.NotFound(new { error = "Диалог не найден." });
            }

            var scenario = catalog.GetById(session.Scenario);
            var messages = store.Messages.Values
                .Where(m => m.SessionId == sessionId)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new
                {
                    m.Id,
                    m.Role,
                    m.Content,
                    m.UsedHint,
                    m.IsVoice,
                    m.ReplyToMessageId,
                    m.CreatedAt
                })
                .ToList();

            return Results.Ok(new
            {
                session.Id,
                session.Scenario,
                session.ScenarioTitle,
                session.Status,
                session.Mode,
                session.LanguageCode,
                session.Level,
                session.UsedHintsCount,
                session.IndependentTurnsCount,
                scenario = scenario != null ? new
                {
                    scenario.Id,
                    scenario.Title,
                    scenario.Description,
                    scenario.BotRole,
                    scenario.UserGoal
                } : null,
                messages
            });
        });

        // 4. Отправка сообщения и ответ бота (время отклика ≤ 3 сек)
        app.MapPost("/api/dialogues/{sessionId:guid}/messages", async (
            Guid sessionId,
            SendMessageRequest req,
            HttpContext ctx,
            LinguaDataStore store,
            ScenarioCatalog catalog,
            GeminiService gemini,
            IntelligentLanguageEngine engine,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Content))
            {
                return Results.BadRequest(new { error = "Сообщение не может быть пустым." });
            }

            if (!store.Sessions.TryGetValue(sessionId, out var session))
            {
                return Results.NotFound(new { error = "Диалог не найден." });
            }

            if (session.Status != "Active")
            {
                return Results.BadRequest(new { error = "Этот диалог уже завершён." });
            }

            bool usedHint = req.UsedHint ?? false;
            if (usedHint)
            {
                session.UsedHintsCount++;
            }
            else
            {
                session.IndependentTurnsCount++;
            }

            // Сохраняем сообщение пользователя
            var userMsg = new DialogueMessage
            {
                SessionId = session.Id,
                Role = "user",
                Content = req.Content.Trim(),
                UsedHint = usedHint,
                IsVoice = req.IsVoice ?? false,
                ReplyToMessageId = req.ReplyToMessageId
            };

            store.Messages[userMsg.Id] = userMsg;
            session.Messages.Add(userMsg);

            // Формируем историю для генерации ответа
            var history = store.Messages.Values
                .Where(m => m.SessionId == sessionId)
                .OrderBy(m => m.CreatedAt)
                .Take(20)
                .ToList();

            var scenario = catalog.GetById(session.Scenario);

            // Генерация реплики бота (Gemini или встроенный интеллектуальный движок)
            string botReply;
            try
            {
                var prompt = new System.Text.StringBuilder();
                prompt.AppendLine("You are LinguaBot, a language learning tutor.");
                prompt.AppendLine($"Dialogue scenario: {session.Scenario}");
                prompt.AppendLine($"Learning language: {session.LanguageCode}");
                prompt.AppendLine($"User level: {session.Level}");
                if (session.Mode == "NoCheatSheet") prompt.AppendLine("Mode: NoCheatSheet (test user's spontaneous formulation)");
                if (scenario != null)
                {
                    prompt.AppendLine($"Bot role: {scenario.BotRole}");
                    prompt.AppendLine($"User goal: {scenario.UserGoal}");
                }
                foreach (var h in history)
                {
                    prompt.AppendLine($"{(h.Role == "user" ? "User" : "Assistant")}: {h.Content}");
                }

                botReply = await gemini.GenerateResponseAsync(prompt.ToString(), ct);
            }
            catch
            {
                botReply = engine.GenerateDialogueReply(session.Scenario, session.LanguageCode, session.Level, userMsg.Content, history.Count, session.Mode == "NoCheatSheet");
            }

            var assistantMsg = new DialogueMessage
            {
                SessionId = session.Id,
                Role = "assistant",
                Content = botReply
            };

            store.Messages[assistantMsg.Id] = assistantMsg;
            session.Messages.Add(assistantMsg);
            store.SaveToDisk();

            return Results.Ok(new
            {
                userMessage = new
                {
                    userMsg.Id,
                    userMsg.Role,
                    userMsg.Content,
                    userMsg.UsedHint,
                    userMsg.IsVoice,
                    userMsg.CreatedAt
                },
                assistantMessage = new
                {
                    assistantMsg.Id,
                    assistantMsg.Role,
                    assistantMsg.Content,
                    assistantMsg.CreatedAt
                }
            });
        });

        // 5. Контекстная подсказка (опорные слова и фраза)
        app.MapGet("/api/dialogues/{sessionId:guid}/hints", (Guid sessionId, LinguaDataStore store, IntelligentLanguageEngine engine) =>
        {
            if (!store.Sessions.TryGetValue(sessionId, out var session))
            {
                return Results.NotFound(new { error = "Диалог не найден." });
            }

            var hints = engine.GetContextHints(session.Scenario, session.LanguageCode, session.Level);
            return Results.Ok(new
            {
                keywords = hints.Keywords,
                examplePhrase = hints.ExamplePhrase
            });
        });

        // 6. Завершение диалога и запуск анализа
        app.MapPost("/api/dialogues/{sessionId:guid}/finish", async (Guid sessionId, LinguaDataStore store, DialogueAnalysisService analysisService, CancellationToken ct) =>
        {
            if (!store.Sessions.TryGetValue(sessionId, out var session))
            {
                return Results.NotFound(new { error = "Диалог не найден." });
            }

            session.Status = "Completed";
            session.FinishedAt = DateTime.UtcNow;

            var analysis = await analysisService.AnalyzeAsync(sessionId, ct);
            store.SaveToDisk();

            return Results.Ok(new
            {
                message = "Диалог успешно завершён.",
                analysisId = analysis.Id,
                status = session.Status
            });
        });

        // 7. Получение анализа диалога и разбора ошибок
        app.MapGet("/api/dialogues/{sessionId:guid}/analysis", async (Guid sessionId, LinguaDataStore store, DialogueAnalysisService analysisService, CancellationToken ct) =>
        {
            if (!store.Sessions.TryGetValue(sessionId, out var session))
            {
                return Results.NotFound(new { error = "Диалог не найден." });
            }

            var analysis = await analysisService.AnalyzeAsync(sessionId, ct);
            var errors = store.Errors.Values
                .Where(e => e.SessionId == sessionId)
                .OrderByDescending(e => e.Severity)
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

            return Results.Ok(new
            {
                analysis.Id,
                analysis.SessionId,
                analysis.OverallFeedback,
                analysis.Strengths,
                analysis.RecommendedLevel,
                sessionMode = session.Mode,
                usedHintsCount = session.UsedHintsCount,
                independentTurnsCount = session.IndependentTurnsCount,
                errors
            });
        });

        // 8. Режим «Без заготовки»: окно «Новая попытка»
        app.MapPost("/api/dialogues/{sessionId:guid}/retry", (Guid sessionId, HttpContext ctx, LinguaDataStore store, ScenarioCatalog catalog) =>
        {
            if (!store.Sessions.TryGetValue(sessionId, out var prevSession))
            {
                return Results.NotFound(new { error = "Исходный диалог не найден." });
            }

            var user = AuthEndpoints.GetOrCreateCurrentUser(ctx, store);

            // Создаем новую сессию в режиме "NoCheatSheet"
            var newSession = new DialogueSession
            {
                UserId = user.Id,
                Scenario = prevSession.Scenario,
                ScenarioTitle = prevSession.ScenarioTitle,
                LanguageCode = prevSession.LanguageCode,
                Level = prevSession.Level,
                Mode = "NoCheatSheet",
                Status = "Active",
                StartedAt = DateTime.UtcNow
            };

            var welcome = new DialogueMessage
            {
                SessionId = newSession.Id,
                Role = "assistant",
                Content = "Режим «Без заготовки»: попробуйте ответить на вопрос своими словами, объяснив причину или подробности без использования шаблонов и подсказок!"
            };

            newSession.Messages.Add(welcome);
            store.Sessions[newSession.Id] = newSession;
            store.Messages[welcome.Id] = welcome;
            store.SaveToDisk();

            return Results.Ok(new
            {
                sessionId = newSession.Id,
                mode = newSession.Mode,
                welcomeMessage = welcome.Content
            });
        });

        // 9. История диалогов пользователя
        app.MapGet("/api/dialogues/history", (HttpContext ctx, LinguaDataStore store, ScenarioCatalog catalog) =>
        {
            var user = AuthEndpoints.GetOrCreateCurrentUser(ctx, store);
            var history = store.Sessions.Values
                .Where(s => s.UserId == user.Id)
                .OrderByDescending(s => s.StartedAt)
                .Select(s =>
                {
                    var sc = catalog.GetById(s.Scenario);
                    int msgCount = store.Messages.Values.Count(m => m.SessionId == s.Id);
                    int errCount = store.Errors.Values.Count(e => e.SessionId == s.Id);
                    int totalTurns = s.IndependentTurnsCount + s.UsedHintsCount;
                    int indepPct = totalTurns > 0 ? (int)Math.Round((double)s.IndependentTurnsCount / totalTurns * 100) : 100;

                    return new
                    {
                        sessionId = s.Id,
                        scenarioId = s.Scenario,
                        scenarioTitle = sc?.Title ?? s.ScenarioTitle,
                        icon = sc?.Icon ?? "💬",
                        status = s.Status,
                        mode = s.Mode,
                        languageCode = s.LanguageCode,
                        level = s.Level,
                        startedAt = s.StartedAt,
                        finishedAt = s.FinishedAt,
                        messagesCount = msgCount,
                        usedHintsCount = s.UsedHintsCount,
                        independentTurnsCount = s.IndependentTurnsCount,
                        independencePercent = indepPct,
                        errorsCount = errCount
                    };
                })
                .ToList();

            return Results.Ok(new { history });
        });
    }
}

public static class OnboardingEndpointsExtension
{
    public static string MapOnboardingEndpointsExtensionGetWelcome(string scenarioId, string lang)
    {
        if (lang == "es")
        {
            return scenarioId switch
            {
                "cafe" => "¡Hola! Bienvenido a nuestra cafetería. ¿Qué le gustaría tomar hoy?",
                "shop" or "shopping" => "¡Hola! ¿Busca alguna prenda en particular o desea echar un vistazo a la nueva colección?",
                "directions" => "¡Hola! Parece que está buscando un lugar. ¿Le puedo ayudar a encontrar la dirección?",
                "transport" => "¡Siguiente en la ventanilla, por favor! ¿A qué destino desea viajar hoy?",
                "hotel" => "¡Buenas tardes! Bienvenido a nuestro hotel. ¿Tiene una reserva con nosotros?",
                "trip" or "trip_planning" => "¡Hola! Me alegra mucho preparar este viaje contigo. ¿Qué destino deberíamos visitar primero?",
                "interview" or "job_interview" => "Buenos días, gracias por venir a la entrevista. Para comenzar, ¿podría presentarse brevemente?",
                "business-meeting" or "business_meeting" => "Buenos días a todos. Comenzamos la reunión de seguimiento del proyecto. ¿Revisamos los puntos clave?",
                "film" => "¡Hola! Acabo de ver esa película de la que todos hablan. ¿Has visto alguna película interesante últimamente?",
                _ => "¡Hola! Bienvenido a nuestro club de idiomas. ¿Cómo te llamas y de dónde eres?"
            };
        }
        if (lang == "pl")
        {
            return scenarioId switch
            {
                "cafe" => "Dzień dobry! Witamy w naszej kawiarni. Na co masz dzisiaj ochotę?",
                "shop" or "shopping" => "Dzień dobry! Czy szukasz czegoś konkretnego, czy po prostu przeglądasz?",
                "directions" => "Przepraszam! Czy potrzebujesz pomocy w odnalezieniu drogi? Dokąd zmierzasz?",
                "transport" => "Następny pasażer, proszę! Dokąd chciałbyś dzisiaj pojechać?",
                "hotel" => "Dzień dobry! Witamy w hotelu. Czy masz u nas rezerwację?",
                "trip" or "trip_planning" => "Cześć! Cieszę się na naszą podróż. Co powinniśmy odwiedzić w pierwszej kolejności?",
                "interview" or "job_interview" => "Dzień dobry, dziękujemy za przybycie. Na początek opowiedz krótko o sobie.",
                "business-meeting" or "business_meeting" => "Dzień dobry wszystkim! Zaczynamy spotkanie projektowe od krótkiego podsumowania zadań.",
                "film" => "Cześć! Oglądałeś ostatnio jakiś ciekawy film? Chętnie o tym porozmawiam.",
                _ => "Cześć! Miło cię poznać w LinguaBot. Opowiedz mi trochę o sobie!"
            };
        }
        if (lang == "ru")
        {
            return scenarioId switch
            {
                "cafe" => "Здравствуйте! Добро пожаловать. Что для вас приготовить сегодня?",
                "shop" or "shopping" => "Добрый день! Ищете что-то конкретное или хотите посмотреть новую коллекцию?",
                "directions" => "Здравствуйте! Выглядите растерянно, подсказать дорогу?",
                "transport" => "Следующий пассажир в кассу, пожалуйста! Куда оформляем билет?",
                "hotel" => "Добрый день! Рады приветствовать вас в нашем отеле. Вы бронировали номер?",
                "trip" or "trip_planning" => "Привет! Я в предвкушении нашей поездки. Какой город или страну посмотрим в первую очередь?",
                "interview" or "job_interview" => "Добрый день, спасибо, что пришли на собеседование. Для начала расскажите немного о себе.",
                "business-meeting" or "business_meeting" => "Доброе утро, коллеги. Начнем со статуса ключевых задач по проекту.",
                "film" => "Привет! Только что досмотрел нашумевший фильм. Что интересного смотрел в последнее время?",
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

public record CreateDialogueSessionRequest(
    string? ScenarioId,
    string? TargetLanguage,
    string? Level,
    string? Goal,
    string? Mode
);

public record SendMessageRequest(
    string Content,
    bool? UsedHint = false,
    bool? IsVoice = false,
    Guid? ReplyToMessageId = null
);
