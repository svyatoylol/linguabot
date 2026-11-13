
namespace LinguaBot.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public UserSettings Settings { get; set; } = new();

    public ICollection<DialogueSession> Sessions { get; set; }
        = new List<DialogueSession>();
}

public class UserSettings
{
    public Guid UserId { get; set; }

    public string NativeLanguage { get; set; } = "ru";

    public string LearningLanguage { get; set; } = "en";

    public string Level { get; set; } = "A1-A2";

    public User User { get; set; } = null!;
}

public class DialogueSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public string Scenario { get; set; } = "";

    public string Status { get; set; } = "Active";

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? FinishedAt { get; set; }

    public User User { get; set; } = null!;

    public ICollection<DialogueMessage> Messages { get; set; }
        = new List<DialogueMessage>();

    public DialogueAnalysis? Analysis { get; set; }

    public ICollection<DetectedError> DetectedErrors { get; set; }
        = new List<DetectedError>();
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
}
