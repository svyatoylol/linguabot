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
    string LevelGuidance,
    string Category = "everyday",
    string Tag = "Повседневное",
    string Icon = "💬",
    string Tone = "blue"
);
