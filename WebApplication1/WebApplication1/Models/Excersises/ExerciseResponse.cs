namespace LinguaBot.Models.Exercises;

public class ExerciseResponse
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string Type { get; set; } = "MultipleChoice";
    public string Category { get; set; } = "Grammar";
    public string Level { get; set; } = "A1-A2";
    public string Question { get; set; } = "";
    public List<string> Options { get; set; } = new();
    public string Explanation { get; set; } = "";
    public string? AudioText { get; set; }
    public int Order { get; set; }
}
