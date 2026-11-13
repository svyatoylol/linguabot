
using LinguaBot.Models;
using Microsoft.EntityFrameworkCore;

namespace LinguaBot.Data;

public class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options
) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<UserSettings> UserSettings => Set<UserSettings>();

    public DbSet<DialogueSession> DialogueSessions => Set<DialogueSession>();

    public DbSet<DialogueMessage> DialogueMessages => Set<DialogueMessage>();

    public DbSet<DialogueAnalysis> DialogueAnalyses => Set<DialogueAnalysis>();

    public DbSet<DetectedError> DetectedErrors => Set<DetectedError>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .HasOne(u => u.Settings)
            .WithOne(s => s.User)
            .HasForeignKey<UserSettings>(s => s.UserId);

        modelBuilder.Entity<UserSettings>()
            .HasKey(s => s.UserId);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.CreatedAt);

        modelBuilder.Entity<DialogueSession>()
            .HasMany(s => s.Messages)
            .WithOne(m => m.Session)
            .HasForeignKey(m => m.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DialogueMessage>()
            .HasOne(m => m.ReplyToMessage)
            .WithMany(m => m.Replies)
            .HasForeignKey(m => m.ReplyToMessageId)
            .OnDelete(DeleteBehavior.Restrict);

        // Одна сессия может иметь только один итоговый анализ.
        modelBuilder.Entity<DialogueSession>()
            .HasOne(s => s.Analysis)
            .WithOne(a => a.Session)
            .HasForeignKey<DialogueAnalysis>(a => a.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DialogueAnalysis>()
            .HasIndex(a => a.SessionId)
            .IsUnique();

        // Ошибки принадлежат конкретной сессии.
        modelBuilder.Entity<DialogueSession>()
            .HasMany(s => s.DetectedErrors)
            .WithOne(e => e.Session)
            .HasForeignKey(e => e.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DialogueMessage>()
            .HasIndex(m => new { m.SessionId, m.CreatedAt });

        modelBuilder.Entity<DetectedError>()
            .HasIndex(e => new { e.SessionId, e.Category });
    }
}
