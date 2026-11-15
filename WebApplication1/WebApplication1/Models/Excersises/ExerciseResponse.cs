namespace LinguaBot.Models.Exercises;

public class ExerciseResponse
{
    public Guid Id { get; set; }
    public string Type { get; set; } = "";
    public string Question { get; set; } = "";
    public List<string> Options { get; set; } = new();
    public int Order { get; set; }
}