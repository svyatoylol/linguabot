using System.Text.Json.Serialization;

namespace LinguaBot.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Гость";
    public string? Email { get; set; }
    public string? PasswordHash { get; set; }
    public string? Phone { get; set; }
    public string Role { get; set; } = "travel"; // travel | work | study
    public bool RemindersEnabled { get; set; } = false;
    public string RemindersTime { get; set; } = "19:00";
    public string TimeZone { get; set; } = "UTC+3";
    public int StreakDays { get; set; } = 1;
    public int DaysLearning { get; set; } = 1;
    public DateTime? LastActiveDate { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public UserSettings Settings { get; set; } = new();

    public ICollection<DialogueSession> Sessions { get; set; } = new List<DialogueSession>();
    public ICollection<UserLanguageProfile> LanguageProfiles { get; set; } = new List<UserLanguageProfile>();
    public ICollection<PlacementTestSession> PlacementTests { get; set; } = new List<PlacementTestSession>();
    public ICollection<DictionaryWord> DictionaryWords { get; set; } = new List<DictionaryWord>();
    public ICollection<DailyPlanItem> DailyPlanItems { get; set; } = new List<DailyPlanItem>();
    public ICollection<UserAchievement> Achievements { get; set; } = new List<UserAchievement>();
    public ICollection<PersonalRecommendation> Recommendations { get; set; } = new List<PersonalRecommendation>();
}

public class UserSettings
{
    public Guid UserId { get; set; }

    // Язык интерфейса и пояснений по умолчанию: ru, en, es
    public string NativeLanguage { get; set; } = "ru";

    // Изучаемый язык и уровень
    public string LearningLanguage { get; set; } = "en";
    public string Level { get; set; } = "A1-A2";

    [JsonIgnore]
    public User User { get; set; } = null!;
}

public class UserLanguageProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    public bool IsSelected { get; set; } = true;

    // ISO-код языка: ru, en, es
    public string LanguageCode { get; set; } = "en";

    // Уровень, выбранный пользователем (A1-A2, B1-B2, C1)
    public string SelectedLevel { get; set; } = "A1-A2";

    // Уровень, подтверждённый тестированием
    public string? CurrentLevel { get; set; }

    public bool IsPlacementCompleted { get; set; }

    public int GrammarScore { get; set; } = 65;
    public int SpeakingScore { get; set; } = 60;
    public int PhoneticsScore { get; set; } = 55;
    public int VocabScore { get; set; } = 70;
    public int WritingScore { get; set; } = 65;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastTestedAt { get; set; }

    [JsonIgnore]
    public User User { get; set; } = null!;

    public ICollection<PlacementTestSession> PlacementTests { get; set; } = new List<PlacementTestSession>();
}

public class DialogueSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    public string Scenario { get; set; } = "introduction";
    public string ScenarioTitle { get; set; } = "Знакомство";
    public string Status { get; set; } = "Active"; // Active | Completed

    // Режим: "Standard" или "NoCheatSheet" (Без заготовки)
    public string Mode { get; set; } = "Standard";

    // Снимок языка и уровня на момент создания диалога
    public string LanguageCode { get; set; } = "en";
    public string Level { get; set; } = "A1-A2";

    // Статистика самостоятельности
    public int UsedHintsCount { get; set; } = 0;
    public int IndependentTurnsCount { get; set; } = 0;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }

    [JsonIgnore]
    public User User { get; set; } = null!;

    public ICollection<DialogueMessage> Messages { get; set; } = new List<DialogueMessage>();
    public DialogueAnalysis? Analysis { get; set; }
    public ICollection<DetectedError> DetectedErrors { get; set; } = new List<DetectedError>();
    public ICollection<Exercise> Exercises { get; set; } = new List<Exercise>();
}

public class DialogueMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid? ReplyToMessageId { get; set; }

    [JsonIgnore]
    public DialogueMessage? ReplyToMessage { get; set; }
    public ICollection<DialogueMessage> Replies { get; set; } = new List<DialogueMessage>();

    public string Role { get; set; } = "user"; // user | assistant | system
    public string Content { get; set; } = "";
    public bool UsedHint { get; set; } = false;
    public bool IsVoice { get; set; } = false;
    public string? AudioUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public DialogueSession Session { get; set; } = null!;
}

public class DialogueAnalysis
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }

    public string OverallFeedback { get; set; } = "";
    public string Strengths { get; set; } = "";
    public string RecommendedLevel { get; set; } = "";
    public int ErrorCount { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public DialogueSession Session { get; set; } = null!;
}

public class DetectedError
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }

    public string OriginalText { get; set; } = "";
    public string CorrectedText { get; set; } = "";
    public string Explanation { get; set; } = "";
    public string Category { get; set; } = "Grammar"; // Grammar | Vocabulary | Spelling | WordOrder | Other
    public int Severity { get; set; } = 1;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public DialogueSession Session { get; set; } = null!;
    public ICollection<Exercise> Exercises { get; set; } = new List<Exercise>();
}

public class Exercise
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? SessionId { get; set; }
    public Guid? DetectedErrorId { get; set; }

    public string Category { get; set; } = "Grammar"; // Grammar | Vocabulary | Listening
    public string Level { get; set; } = "A1-A2"; // A1-A2 | B1-B2 | C1
    public string Type { get; set; } = "MultipleChoice"; // MultipleChoice | FillInTheBlank | Translation | Listening

    public string Question { get; set; } = "";
    public string Options { get; set; } = "[]"; // JSON array of options
    public string CorrectAnswer { get; set; } = "";
    public string Explanation { get; set; } = "";
    public string? AudioText { get; set; } // for listening tasks
    public int Order { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public DialogueSession? Session { get; set; }
    [JsonIgnore]
    public DetectedError? DetectedError { get; set; }

    public ICollection<ExerciseAttempt> Attempts { get; set; } = new List<ExerciseAttempt>();
}

public class ExerciseAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExerciseId { get; set; }
    public Guid? UserId { get; set; }

    public string UserAnswer { get; set; } = "";
    public bool IsCorrect { get; set; }
    public bool UsedHint { get; set; } = false;

    public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public Exercise Exercise { get; set; } = null!;
}

public class PlacementTestSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid LanguageProfileId { get; set; }

    public string Status { get; set; } = "InProgress"; // InProgress | Completed | Cancelled
    public string? ResultLevel { get; set; }
    public int TotalQuestions { get; set; } = 12;
    public int CorrectAnswers { get; set; } = 0;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }

    [JsonIgnore]
    public User User { get; set; } = null!;
    [JsonIgnore]
    public UserLanguageProfile LanguageProfile { get; set; } = null!;

    public ICollection<PlacementTestQuestion> Questions { get; set; } = new List<PlacementTestQuestion>();
    public ICollection<PlacementTestAnswer> Answers { get; set; } = new List<PlacementTestAnswer>();
}

public class PlacementTestQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlacementTestSessionId { get; set; }

    public int Order { get; set; }
    public string Skill { get; set; } = "Grammar"; // Grammar | Vocabulary | Reading
    public string Type { get; set; } = "MultipleChoice";
    public string Prompt { get; set; } = "";
    public string Options { get; set; } = "[]";
    public string CorrectAnswer { get; set; } = "";
    public string Explanation { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public PlacementTestSession PlacementTestSession { get; set; } = null!;
    public ICollection<PlacementTestAnswer> Answers { get; set; } = new List<PlacementTestAnswer>();
}

public class PlacementTestAnswer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlacementTestSessionId { get; set; }
    public Guid PlacementTestQuestionId { get; set; }

    public string UserAnswer { get; set; } = "";
    public bool IsCorrect { get; set; }
    public DateTime AnsweredAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public PlacementTestSession PlacementTestSession { get; set; } = null!;
    [JsonIgnore]
    public PlacementTestQuestion PlacementTestQuestion { get; set; } = null!;
}

// Личный словарь пользователя с алгоритмом интервального повторения (SRS)
public class DictionaryWord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    public string Word { get; set; } = "";
    public string Translation { get; set; } = "";
    public string Transcription { get; set; } = "";
    public string ContextPhrase { get; set; } = "";
    public string LanguageCode { get; set; } = "en";

    // SRS интервалы: 1 день -> 3 дня -> 7 дней -> 14 дней -> 30 дней
    public int RepetitionIntervalDays { get; set; } = 1;
    public int ConsecutiveCorrectAnswers { get; set; } = 0;
    public bool IsMastered { get; set; } = false; // "Изучено" только при отложенном повторении без подсказки!

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public DateTime NextReviewAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public User User { get; set; } = null!;
}

// Элемент чек-листа раздела «Сегодня»
public class DailyPlanItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    public int StepIndex { get; set; } // 1, 2, 3, 4
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string Type { get; set; } = "repetition"; // repetition | words | grammar | dialogue
    public string ActionTarget { get; set; } = "";
    public bool IsCompleted { get; set; } = false;
    public DateTime? CompletedAt { get; set; }
    public string DayDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd");

    [JsonIgnore]
    public User User { get; set; } = null!;
}

// Бейджи достижений
public class UserAchievement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "🏆";
    public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public User User { get; set; } = null!;
}

// Персональные рекомендации
public class PersonalRecommendation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    public string Topic { get; set; } = "";
    public string Description { get; set; } = "";
    public string RecommendedAction { get; set; } = "exercise"; // exercise | dialogue | grammar
    public string TargetId { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public User User { get; set; } = null!;
}
