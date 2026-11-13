
namespace LinguaBot.Models;

public record ScenarioDefinition(
    string Id,
    string Title,
    string Description,
    string BotRole,
    string UserGoal,
    string Vocabulary,
    string DialogueFlow,
    string CompletionCriteria,
    string LevelGuidance
);
