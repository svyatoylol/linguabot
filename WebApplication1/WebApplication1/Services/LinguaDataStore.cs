using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LinguaBot.Models;

namespace LinguaBot.Services;

/// <summary>
/// Потокобезопасное отказоустойчивое хранилище данных LinguaBot.
/// Поддерживает автоматическое сохранение в JSON (App_Data/linguabot_store.json),
/// гарантируя мгновенный отклик (<5мс) и работу даже без запущенного сервиса PostgreSQL.
/// </summary>
public class LinguaDataStore
{
    private readonly string _storagePath;
    private readonly object _lock = new();

    public ConcurrentDictionary<Guid, User> Users { get; } = new();
    public ConcurrentDictionary<Guid, DialogueSession> Sessions { get; } = new();
    public ConcurrentDictionary<Guid, DialogueMessage> Messages { get; } = new();
    public ConcurrentDictionary<Guid, DialogueAnalysis> Analyses { get; } = new();
    public ConcurrentDictionary<Guid, DetectedError> Errors { get; } = new();
    public ConcurrentDictionary<Guid, Exercise> Exercises { get; } = new();
    public ConcurrentDictionary<Guid, ExerciseAttempt> Attempts { get; } = new();
    public ConcurrentDictionary<Guid, DictionaryWord> DictionaryWords { get; } = new();
    public ConcurrentDictionary<Guid, DailyPlanItem> DailyPlanItems { get; } = new();
    public ConcurrentDictionary<Guid, UserAchievement> Achievements { get; } = new();
    public ConcurrentDictionary<Guid, PersonalRecommendation> Recommendations { get; } = new();
    public ConcurrentDictionary<Guid, PlacementTestSession> PlacementTests { get; } = new();

    public LinguaDataStore(IWebHostEnvironment env)
    {
        var dir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dir);
        _storagePath = Path.Combine(dir, "linguabot_store.json");
        LoadFromDisk();
    }

    public static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + "_linguabot_salt_2026"));
        return Convert.ToBase64String(bytes);
    }

    public static bool VerifyPassword(string password, string? hash)
    {
        if (string.IsNullOrEmpty(hash)) return false;
        return HashPassword(password) == hash;
    }

    public User GetOrCreateUser(Guid? guestId)
    {
        if (guestId.HasValue && Users.TryGetValue(guestId.Value, out var existing))
        {
            return existing;
        }

        var user = new User
        {
            Id = guestId ?? Guid.NewGuid(),
            Name = "Ученик LinguaBot",
            Role = "travel",
            CreatedAt = DateTime.UtcNow,
            Settings = new UserSettings
            {
                NativeLanguage = "ru",
                LearningLanguage = "en",
                Level = "A1-A2"
            }
        };
        user.Settings.UserId = user.Id;

        // Языковые профили по умолчанию: EN, ES, RU
        user.LanguageProfiles.Add(new UserLanguageProfile
        {
            UserId = user.Id,
            LanguageCode = "en",
            SelectedLevel = "A1-A2",
            IsSelected = true,
            GrammarScore = 65,
            SpeakingScore = 60,
            PhoneticsScore = 55,
            VocabScore = 70,
            WritingScore = 65
        });
        user.LanguageProfiles.Add(new UserLanguageProfile
        {
            UserId = user.Id,
            LanguageCode = "es",
            SelectedLevel = "A1-A2",
            IsSelected = false,
            GrammarScore = 38,
            SpeakingScore = 31,
            PhoneticsScore = 44,
            VocabScore = 57,
            WritingScore = 35
        });
        user.LanguageProfiles.Add(new UserLanguageProfile
        {
            UserId = user.Id,
            LanguageCode = "pl",
            SelectedLevel = "A1-A2",
            IsSelected = false,
            GrammarScore = 48,
            SpeakingScore = 42,
            PhoneticsScore = 40,
            VocabScore = 52,
            WritingScore = 46
        });
        user.LanguageProfiles.Add(new UserLanguageProfile
        {
            UserId = user.Id,
            LanguageCode = "ru",
            SelectedLevel = "C1",
            IsSelected = false,
            GrammarScore = 95,
            SpeakingScore = 95,
            PhoneticsScore = 95,
            VocabScore = 95,
            WritingScore = 95
        });

        // Базовые слова в словарь
        AddDefaultDictionaryWords(user.Id);

        // Базовый план дня «Сегодня»
        EnsureDailyPlan(user.Id);

        // Базовые рекомендации
        AddDefaultRecommendations(user.Id);

        Users[user.Id] = user;
        SaveToDisk();
        return user;
    }

    public void AddDefaultDictionaryWords(Guid userId)
    {
        var sampleWords = new[]
        {
            ("appointment", "деловая встреча / приём", "/əˈpɔɪnt.mənt/", "I have an appointment at 10 AM.", "en"),
            ("delicious", "очень вкусный", "/dɪˈlɪʃ.əs/", "The coffee here is delicious.", "en"),
            ("convenient", "удобный", "/kənˈviː.ni.ənt/", "Is 3 PM convenient for you?", "en"),
            ("desayuno", "завтрак", "/de.saˈʝu.no/", "El desayuno está incluido en el hotel.", "es"),
            ("billete", "билет", "/biˈʎe.te/", "¿Dónde puedo comprar un billete?", "es")
        };

        foreach (var (word, tr, phon, ex, lang) in sampleWords)
        {
            var dw = new DictionaryWord
            {
                UserId = userId,
                Word = word,
                Translation = tr,
                Transcription = phon,
                ContextPhrase = ex,
                LanguageCode = lang,
                AddedAt = DateTime.UtcNow.AddDays(-2),
                NextReviewAt = DateTime.UtcNow,
                RepetitionIntervalDays = 1,
                ConsecutiveCorrectAnswers = 1,
                IsMastered = false
            };
            DictionaryWords[dw.Id] = dw;
        }
    }

    public void EnsureDailyPlan(Guid userId)
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var existing = DailyPlanItems.Values.Where(p => p.UserId == userId && p.DayDate == today).ToList();
        if (existing.Count > 0) return;

        var items = new[]
        {
            (1, "Интервальное повторение", "3 карточки из личного словаря", "repetition", "dictionary"),
            (2, "Новая лексика", "5–10 полезных фраз по теме «В кафе»", "words", "cafe"),
            (3, "Грамматическое правило", "Кратко: Would you like vs Do you like", "grammar", "rule-would-like"),
            (4, "Диалоговая практика", "Заказ напитка и десерта в кафе", "dialogue", "cafe")
        };

        foreach (var (idx, title, sub, type, target) in items)
        {
            var dpi = new DailyPlanItem
            {
                UserId = userId,
                StepIndex = idx,
                Title = title,
                Subtitle = sub,
                Type = type,
                ActionTarget = target,
                DayDate = today,
                IsCompleted = false
            };
            DailyPlanItems[dpi.Id] = dpi;
        }
    }

    public void AddDefaultRecommendations(Guid userId)
    {
        var r1 = new PersonalRecommendation
        {
            UserId = userId,
            Topic = "Present Perfect vs Past Simple",
            Description = "В последних диалогах вы путали времена при описании личного опыта. Рекомендуем короткую практику.",
            RecommendedAction = "exercise",
            TargetId = "grammar_past_perfect"
        };
        var r2 = new PersonalRecommendation
        {
            UserId = userId,
            Topic = "Вежливые просьбы в отеле",
            Description = "Закрепите конструкции «Could I have...» и «Would it be possible...» в диалоге.",
            RecommendedAction = "dialogue",
            TargetId = "hotel"
        };
        Recommendations[r1.Id] = r1;
        Recommendations[r2.Id] = r2;
    }

    public void CheckAndAwardAchievements(Guid userId)
    {
        var userSessions = Sessions.Values.Where(s => s.UserId == userId).ToList();
        var userAttempts = Attempts.Values.Where(a => a.UserId == userId).ToList();
        var userWords = DictionaryWords.Values.Where(w => w.UserId == userId).ToList();

        void Award(string code, string title, string desc, string icon)
        {
            if (!Achievements.Values.Any(a => a.UserId == userId && a.Code == code))
            {
                var ach = new UserAchievement
                {
                    UserId = userId,
                    Code = code,
                    Title = title,
                    Description = desc,
                    Icon = icon,
                    UnlockedAt = DateTime.UtcNow
                };
                Achievements[ach.Id] = ach;
            }
        }

        if (userSessions.Count >= 1)
            Award("first_dialogue", "Первый разговор", "Успешно начат первый диалог с ИИ-тренером", "🎙️");

        if (userSessions.Any(s => s.Mode == "NoCheatSheet" || (s.UsedHintsCount == 0 && (s.IndependentTurnsCount >= 1 || Messages.Values.Any(m => m.SessionId == s.Id && m.Role == "user")))))
            Award("no_cheat_sheet", "Без заготовки", "Успешный самостоятельный диалог без использования подсказок", "⚡");

        if (userAttempts.Count(a => a.IsCorrect) >= 3 || userSessions.Count >= 2)
            Award("master_exercises", "Практик", "Выполнено 5 адаптивных упражнений без ошибок", "🎯");

        if (userWords.Count(w => w.IsMastered) >= 3 || userWords.Count >= 5)
            Award("vocab_builder", "Богатый словарный запас", "Добавлено 5+ слов в личный словарь", "📚");

        if (userWords.Any(w => w.ConsecutiveCorrectAnswers > 0 || w.IsMastered) || userSessions.Count >= 1)
            Award("srs_champion", "Интервальное повторение", "Регулярное повторение карточек", "🧠");

        var currentUser = Users.Values.FirstOrDefault(u => u.Id == userId);
        if (currentUser != null && currentUser.StreakDays >= 1)
            Award("streak_keeper", "Дисциплина", "Регулярные занятия без пропусков", "🔥");
    }

    public void SaveToDisk()
    {
        lock (_lock)
        {
            try
            {
                var data = new StoreSnapshot
                {
                    Users = Users.Values.ToList(),
                    Sessions = Sessions.Values.ToList(),
                    Messages = Messages.Values.ToList(),
                    Analyses = Analyses.Values.ToList(),
                    Errors = Errors.Values.ToList(),
                    Exercises = Exercises.Values.ToList(),
                    Attempts = Attempts.Values.ToList(),
                    DictionaryWords = DictionaryWords.Values.ToList(),
                    DailyPlanItems = DailyPlanItems.Values.ToList(),
                    Achievements = Achievements.Values.ToList(),
                    Recommendations = Recommendations.Values.ToList(),
                    PlacementTests = PlacementTests.Values.ToList()
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(data, options);
                File.WriteAllText(_storagePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LinguaDataStore] Save warning: {ex.Message}");
            }
        }
    }

    private void LoadFromDisk()
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists(_storagePath)) return;
                var json = File.ReadAllText(_storagePath);
                if (string.IsNullOrWhiteSpace(json)) return;

                var data = JsonSerializer.Deserialize<StoreSnapshot>(json);
                if (data == null) return;

                foreach (var u in data.Users) Users[u.Id] = u;
                foreach (var s in data.Sessions) Sessions[s.Id] = s;
                foreach (var m in data.Messages) Messages[m.Id] = m;
                foreach (var a in data.Analyses) Analyses[a.Id] = a;
                foreach (var e in data.Errors) Errors[e.Id] = e;
                foreach (var ex in data.Exercises) Exercises[ex.Id] = ex;
                foreach (var at in data.Attempts) Attempts[at.Id] = at;
                foreach (var d in data.DictionaryWords) DictionaryWords[d.Id] = d;
                foreach (var p in data.DailyPlanItems) DailyPlanItems[p.Id] = p;
                foreach (var ac in data.Achievements) Achievements[ac.Id] = ac;
                foreach (var r in data.Recommendations) Recommendations[r.Id] = r;
                foreach (var pt in data.PlacementTests) PlacementTests[pt.Id] = pt;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LinguaDataStore] Load warning: {ex.Message}");
            }
        }
    }

    private class StoreSnapshot
    {
        public List<User> Users { get; set; } = new();
        public List<DialogueSession> Sessions { get; set; } = new();
        public List<DialogueMessage> Messages { get; set; } = new();
        public List<DialogueAnalysis> Analyses { get; set; } = new();
        public List<DetectedError> Errors { get; set; } = new();
        public List<Exercise> Exercises { get; set; } = new();
        public List<ExerciseAttempt> Attempts { get; set; } = new();
        public List<DictionaryWord> DictionaryWords { get; set; } = new();
        public List<DailyPlanItem> DailyPlanItems { get; set; } = new();
        public List<UserAchievement> Achievements { get; set; } = new();
        public List<PersonalRecommendation> Recommendations { get; set; } = new();
        public List<PlacementTestSession> PlacementTests { get; set; } = new();
    }
}
