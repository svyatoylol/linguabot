
namespace LinguaBot.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public UserSettings Settings { get; set; } = new();

    public ICollection<DialogueSession> Sessions { get; set; }
        = new List<DialogueSession>();

    public ICollection<UserLanguageProfile> LanguageProfiles { get; set; }
        = new List<UserLanguageProfile>();

    public ICollection<PlacementTestSession> PlacementTests { get; set; }
        = new List<PlacementTestSession>();
}

public class UserSettings
{
    public Guid UserId { get; set; }

    // Язык интерфейса и объяснений по умолчанию.
    public string NativeLanguage { get; set; } = "ru";

    // Временные поля для совместимости со старой архитектурой.
    // В дальнейшем выбор языка будет выполняться через UserLanguageProfile.
    public string LearningLanguage { get; set; } = "en";
    public string Level { get; set; } = "A1-A2";

    public User User { get; set; } = null!;
}

public class UserLanguageProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    public bool IsSelected { get; set; } = true;

    // ISO-код языка: ru, en, pl.
    public string LanguageCode { get; set; } = "en";

    // Уровень, который пользователь выбрал самостоятельно.
    public string SelectedLevel { get; set; } = "A1-A2";

    // Уровень, подтверждённый тестированием.
    public string? CurrentLevel { get; set; }

    public bool IsPlacementCompleted { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastTestedAt { get; set; }

    public User User { get; set; } = null!;

    public ICollection<PlacementTestSession> PlacementTests { get; set; }
        = new List<PlacementTestSession>();

}

public class DialogueSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    public string Scenario { get; set; } = "";
    public string Status { get; set; } = "Active";

    // Снимок языка и уровня на момент создания диалога.
    public string LanguageCode { get; set; } = "en";
    public string Level { get; set; } = "A1-A2";

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }

    public User User { get; set; } = null!;

    public ICollection<DialogueMessage> Messages { get; set; }
        = new List<DialogueMessage>();

    public DialogueAnalysis? Analysis { get; set; }

    public ICollection<DetectedError> DetectedErrors { get; set; }
        = new List<DetectedError>();

    public ICollection<Exercise> Exercises { get; set; }
        = new List<Exercise>();
}

public class DialogueMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid? ReplyToMessageId { get; set; }

    public DialogueMessage? ReplyToMessage { get; set; }

    public ICollection<DialogueMessage> Replies { get; set; }
        = new List<DialogueMessage>();

    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DialogueSession Session { get; set; } = null!;
}

public class DialogueAnalysis
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }

    public string OverallFeedback { get; set; } = "";
    public string Strengths { get; set; } = "";
    public string RecommendedLevel { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DialogueSession Session { get; set; } = null!;
}

public class DetectedError
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }

    public string OriginalText { get; set; } = "";
    public string CorrectedText { get; set; } = "";
    public string Explanation { get; set; } = "";
    public string Category { get; set; } = "Other";
    public int Severity { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DialogueSession Session { get; set; } = null!;

    public ICollection<Exercise> Exercises { get; set; }
        = new List<Exercise>();
}

public class Exercise
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid? DetectedErrorId { get; set; }

    // MultipleChoice, FillInTheBlank, Translation.
    public string Type { get; set; } = "MultipleChoice";

    public string Question { get; set; } = "";

    // JSON-массив вариантов ответа.
    public string Options { get; set; } = "[]";

    // Не возвращать клиенту до отправки ответа.
    public string CorrectAnswer { get; set; } = "";

    public string Explanation { get; set; } = "";
    public int Order { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DialogueSession Session { get; set; } = null!;
    public DetectedError? DetectedError { get; set; }

    public ICollection<ExerciseAttempt> Attempts { get; set; }
        = new List<ExerciseAttempt>();
}

public class ExerciseAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExerciseId { get; set; }

    public string UserAnswer { get; set; } = "";
    public bool IsCorrect { get; set; }

    public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;

    public Exercise Exercise { get; set; } = null!;
}

// Отдельное прохождение теста по одному языку.
public class PlacementTestSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public Guid LanguageProfileId { get; set; }

    // InProgress, Completed, Cancelled.
    public string Status { get; set; } = "InProgress";

    public string? ResultLevel { get; set; }

    public int TotalQuestions { get; set; }
    public int CorrectAnswers { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }

    public User User { get; set; } = null!;
    public UserLanguageProfile LanguageProfile { get; set; } = null!;

    public ICollection<PlacementTestQuestion> Questions { get; set; }
        = new List<PlacementTestQuestion>();

    public ICollection<PlacementTestAnswer> Answers { get; set; }
        = new List<PlacementTestAnswer>();
}

// Вопрос, сохранённый в рамках конкретного тестирования.
public class PlacementTestQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlacementTestSessionId { get; set; }

    public int Order { get; set; }

    // Vocabulary, Grammar, Reading, Listening.
    public string Skill { get; set; } = "Grammar";

    // MultipleChoice, FillInTheBlank, FreeText.
    public string Type { get; set; } = "MultipleChoice";

    public string Prompt { get; set; } = "";

    // JSON-массив вариантов ответа.
    public string Options { get; set; } = "[]";

    // Ответ и объяснение хранятся только на сервере.
    public string CorrectAnswer { get; set; } = "";
    public string Explanation { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public PlacementTestSession PlacementTestSession { get; set; } = null!;

    public ICollection<PlacementTestAnswer> Answers { get; set; }
        = new List<PlacementTestAnswer>();
}

// Попытка пользователя ответить на вопрос.
public class PlacementTestAnswer
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PlacementTestSessionId { get; set; }
    public Guid PlacementTestQuestionId { get; set; }

    public string UserAnswer { get; set; } = "";

    public bool IsCorrect { get; set; }

    public DateTime AnsweredAt { get; set; } = DateTime.UtcNow;

    public PlacementTestSession PlacementTestSession { get; set; } = null!;
    public PlacementTestQuestion PlacementTestQuestion { get; set; } = null!;
}
