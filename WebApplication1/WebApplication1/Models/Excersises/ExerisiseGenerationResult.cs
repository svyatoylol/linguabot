
namespace LinguaBot.Models.Exercises;

public class ExerciseGenerationResult
{
    public List<GeneratedExercise> Exercises { get; set; } = new();
}

public class GeneratedExercise
{
    public string Type { get; set; } = "MultipleChoice";
    public string Question { get; set; } = "";
    public List<string> Options { get; set; } = new();
    public string CorrectAnswer { get; set; } = "";
    public string Explanation { get; set; } = "";
    public Guid? DetectedErrorId { get; set; }
}
