
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
            .HasIndex(a => new { a.PlacementTestSessionId, a.AnsweredAt });

        // DialogueSession -> Messages.
        modelBuilder.Entity<DialogueSession>()
            .HasMany(s => s.Messages)
            .WithOne(m => m.Session)
            .HasForeignKey(m => m.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // DialogueMessage -> ReplyToMessage.
        modelBuilder.Entity<DialogueMessage>()
            .HasOne(m => m.ReplyToMessage)
            .WithMany(m => m.Replies)
            .HasForeignKey(m => m.ReplyToMessageId)
            .OnDelete(DeleteBehavior.Restrict);

        // DialogueSession -> DialogueAnalysis (one-to-one).
        modelBuilder.Entity<DialogueSession>()
            .HasOne(s => s.Analysis)
            .WithOne(a => a.Session)
            .HasForeignKey<DialogueAnalysis>(a => a.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DialogueAnalysis>()
            .HasIndex(a => a.SessionId)
            .IsUnique();

        // DialogueSession -> DetectedErrors.
        modelBuilder.Entity<DialogueSession>()
            .HasMany(s => s.DetectedErrors)
            .WithOne(e => e.Session)
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DialogueMessage>()
            .HasIndex(m => new { m.SessionId, m.CreatedAt });

        modelBuilder.Entity<DetectedError>()
            .HasIndex(e => new { e.SessionId, e.Category });

        // DialogueSession -> Exercises.
        modelBuilder.Entity<DialogueSession>()
            .HasMany(s => s.Exercises)
            .WithOne(e => e.Session)
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // DetectedError -> Exercises.
        modelBuilder.Entity<DetectedError>()
            .HasMany(e => e.Exercises)
            .WithOne(e => e.DetectedError)
            .HasForeignKey(e => e.DetectedErrorId)
            .OnDelete(DeleteBehavior.SetNull);

        // Exercise -> ExerciseAttempts.
        modelBuilder.Entity<Exercise>()
            .HasMany(e => e.Attempts)
            .WithOne(a => a.Exercise)
            .HasForeignKey(a => a.ExerciseId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Exercise>()
            .HasIndex(e => new { e.SessionId, e.Order });

        modelBuilder.Entity<Exercise>()
            .HasIndex(e => e.DetectedErrorId);

        modelBuilder.Entity<ExerciseAttempt>()
            .HasIndex(a => new { a.ExerciseId, a.AttemptedAt });
    }
}
