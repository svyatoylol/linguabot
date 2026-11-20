using LinguaBot.Models;
using Microsoft.EntityFrameworkCore;

namespace LinguaBot.Data;

public class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options
) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();

    public DbSet<UserLanguageProfile> UserLanguageProfiles
        => Set<UserLanguageProfile>();

    public DbSet<DialogueSession> DialogueSessions
        => Set<DialogueSession>();

    public DbSet<DialogueMessage> DialogueMessages
        => Set<DialogueMessage>();

    public DbSet<DialogueAnalysis> DialogueAnalyses
        => Set<DialogueAnalysis>();

    public DbSet<DetectedError> DetectedErrors
        => Set<DetectedError>();

    public DbSet<Exercise> Exercises
        => Set<Exercise>();

    public DbSet<ExerciseAttempt> ExerciseAttempts
        => Set<ExerciseAttempt>();

    public DbSet<PlacementTestSession> PlacementTestSessions
        => Set<PlacementTestSession>();

    public DbSet<PlacementTestQuestion> PlacementTestQuestions
        => Set<PlacementTestQuestion>();

    public DbSet<PlacementTestAnswer> PlacementTestAnswers
        => Set<PlacementTestAnswer>();

    public DbSet<DictionaryWord> DictionaryWords
        => Set<DictionaryWord>();

    public DbSet<DailyPlanItem> DailyPlanItems
        => Set<DailyPlanItem>();

    public DbSet<UserAchievement> Achievements
        => Set<UserAchievement>();

    public DbSet<PersonalRecommendation> Recommendations
        => Set<PersonalRecommendation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User -> UserSettings (one-to-one).
        modelBuilder.Entity<User>()
            .HasOne(u => u.Settings)
            .WithOne(s => s.User)
            .HasForeignKey<UserSettings>(s => s.UserId);

        modelBuilder.Entity<UserSettings>()
            .HasKey(s => s.UserId);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.CreatedAt);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email);

        // User -> UserLanguageProfiles (one-to-many).
        modelBuilder.Entity<User>()
            .HasMany(u => u.LanguageProfiles)
            .WithOne(p => p.User)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserLanguageProfile>()
            .HasIndex(p => new { p.UserId, p.LanguageCode })
            .IsUnique();

        // User -> PlacementTestSessions (one-to-many).
        modelBuilder.Entity<User>()
            .HasMany(u => u.PlacementTests)
            .WithOne(t => t.User)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // UserLanguageProfile -> PlacementTestSessions.
        modelBuilder.Entity<UserLanguageProfile>()
            .HasMany(p => p.PlacementTests)
            .WithOne(t => t.LanguageProfile)
            .HasForeignKey(t => t.LanguageProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PlacementTestSession>()
            .HasIndex(t => new { t.UserId, t.StartedAt });

        modelBuilder.Entity<PlacementTestSession>()
            .HasIndex(t => new { t.LanguageProfileId, t.Status });

        // PlacementTestSession -> Questions.
        modelBuilder.Entity<PlacementTestSession>()
            .HasMany(t => t.Questions)
            .WithOne(q => q.PlacementTestSession)
            .HasForeignKey(q => q.PlacementTestSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PlacementTestQuestion>()
            .HasIndex(q => new { q.PlacementTestSessionId, q.Order })
            .IsUnique();

        // PlacementTestSession -> Answers.
        modelBuilder.Entity<PlacementTestSession>()
            .HasMany(t => t.Answers)
            .WithOne(a => a.PlacementTestSession)
            .HasForeignKey(a => a.PlacementTestSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // PlacementTestQuestion -> Answers.
        modelBuilder.Entity<PlacementTestQuestion>()
            .HasMany(q => q.Answers)
            .WithOne(a => a.PlacementTestQuestion)
            .HasForeignKey(a => a.PlacementTestQuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PlacementTestAnswer>()
            .HasIndex(a => new { a.PlacementTestSessionId, a.PlacementTestQuestionId })
            .IsUnique();

        // User -> Sessions.
        modelBuilder.Entity<DialogueSession>()
            .HasOne(s => s.User)
            .WithMany(u => u.Sessions)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DialogueSession>()
            .HasIndex(s => s.UserId);

        modelBuilder.Entity<DialogueSession>()
            .HasIndex(s => s.StartedAt);

        // DialogueSession -> Messages.
        modelBuilder.Entity<DialogueMessage>()
            .HasOne(m => m.Session)
            .WithMany(s => s.Messages)
            .HasForeignKey(m => m.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DialogueMessage>()
            .HasIndex(m => m.SessionId);

        modelBuilder.Entity<DialogueMessage>()
            .HasIndex(m => m.CreatedAt);

        // DialogueMessage self-referencing reply relation.
        modelBuilder.Entity<DialogueMessage>()
            .HasOne(m => m.ReplyToMessage)
            .WithMany(m => m.Replies)
            .HasForeignKey(m => m.ReplyToMessageId)
            .OnDelete(DeleteBehavior.SetNull);

        // DialogueSession -> Analysis (one-to-one).
        modelBuilder.Entity<DialogueSession>()
            .HasOne(s => s.Analysis)
            .WithOne(a => a.Session)
            .HasForeignKey<DialogueAnalysis>(a => a.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DialogueAnalysis>()
            .HasKey(a => a.Id);

        modelBuilder.Entity<DialogueAnalysis>()
            .HasIndex(a => a.SessionId)
            .IsUnique();

        // DialogueSession -> DetectedErrors.
        modelBuilder.Entity<DetectedError>()
            .HasOne(e => e.Session)
            .WithMany(s => s.DetectedErrors)
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DetectedError>()
            .HasIndex(e => e.SessionId);

        // DialogueSession -> Exercises.
        modelBuilder.Entity<Exercise>()
            .HasOne(e => e.Session)
            .WithMany(s => s.Exercises)
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Exercise>()
            .HasIndex(e => e.SessionId);

        // DetectedError -> Exercises.
        modelBuilder.Entity<Exercise>()
            .HasOne(e => e.DetectedError)
            .WithMany(err => err.Exercises)
            .HasForeignKey(e => e.DetectedErrorId)
            .OnDelete(DeleteBehavior.SetNull);

        // Exercise -> Attempts.
        modelBuilder.Entity<ExerciseAttempt>()
            .HasOne(a => a.Exercise)
            .WithMany(e => e.Attempts)
            .HasForeignKey(a => a.ExerciseId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ExerciseAttempt>()
            .HasIndex(a => a.ExerciseId);

        // User -> DictionaryWords.
        modelBuilder.Entity<DictionaryWord>()
            .HasOne(d => d.User)
            .WithMany(u => u.DictionaryWords)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DictionaryWord>()
            .HasIndex(d => new { d.UserId, d.Word, d.LanguageCode });

        // User -> DailyPlanItems.
        modelBuilder.Entity<DailyPlanItem>()
            .HasOne(p => p.User)
            .WithMany(u => u.DailyPlanItems)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // User -> Achievements.
        modelBuilder.Entity<UserAchievement>()
            .HasOne(a => a.User)
            .WithMany(u => u.Achievements)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // User -> Recommendations.
        modelBuilder.Entity<PersonalRecommendation>()
            .HasOne(r => r.User)
            .WithMany(u => u.Recommendations)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
