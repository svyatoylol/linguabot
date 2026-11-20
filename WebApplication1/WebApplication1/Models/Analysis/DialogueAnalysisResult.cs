
namespace LinguaBot.Models.Analysis;

public class DialogueAnalysisResult
{
    public string OverallFeedback { get; set; } = "";

    public string Strengths { get; set; } = "";

    public string RecommendedLevel { get; set; } = "";

    public List<DetectedErrorResult> Errors { get; set; } = new();
}

public class DetectedErrorResult
{
    public string OriginalText { get; set; } = "";

    public string CorrectedText { get; set; } = "";

    public string Explanation { get; set; } = "";

    public string Category { get; set; } = "Other";

    public int Severity { get; set; }
}
