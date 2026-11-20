using LinguaBot.Models;

namespace LinguaBot.Services;

public class PlacementTestGeneratorService
{
    private readonly IntelligentLanguageEngine _engine;

    public PlacementTestGeneratorService(IntelligentLanguageEngine engine)
    {
        _engine = engine;
    }

    public Task<List<GeneratedPlacementQuestion>> GenerateAsync(
        string languageCode,
        string selectedLevel,
        CancellationToken cancellationToken = default)
    {
        var questions = _engine.GeneratePlacementQuestions(Guid.Empty, languageCode);

        var result = questions.Select(q =>
        {
            List<string> options = new();
            try
            {
                options = System.Text.Json.JsonSerializer.Deserialize<List<string>>(q.Options) ?? new();
            }
            catch { }

            return new GeneratedPlacementQuestion
            {
                Skill = q.Skill,
                Type = q.Type,
                Prompt = q.Prompt,
                Options = options,
                CorrectAnswer = q.CorrectAnswer,
                Explanation = q.Explanation
            };
        }).ToList();

        return Task.FromResult(result);
    }
}

public class GeneratedPlacementQuestion
{
    public string Skill { get; set; } = "Grammar";
    public string Type { get; set; } = "MultipleChoice";
    public string Prompt { get; set; } = "";
    public List<string> Options { get; set; } = new();
    public string CorrectAnswer { get; set; } = "";
    public string Explanation { get; set; } = "";
}
