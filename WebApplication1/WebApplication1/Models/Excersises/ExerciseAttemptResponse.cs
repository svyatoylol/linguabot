namespace LinguaBot.Models.Exercises;

public class ExerciseAttemptResponse
{
    public Guid Id { get; set; }
    public bool IsCorrect { get; set; }
    public int AttemptsCount { get; set; }
    public bool CanRetry { get; set; }
    public string Feedback { get; set; } = "";
    public DateTime AttemptedAt { get; set; }
}