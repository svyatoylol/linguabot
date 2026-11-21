using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication1.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiLanguageProfilesAndPlacementTests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LanguageCode",
                table: "DialogueSessions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Level",
                table: "DialogueSessions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "UserLanguageProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LanguageCode = table.Column<string>(type: "text", nullable: false),
                    SelectedLevel = table.Column<string>(type: "text", nullable: false),
                    CurrentLevel = table.Column<string>(type: "text", nullable: true),
                    IsPlacementCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastTestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLanguageProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserLanguageProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlacementTestSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LanguageProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ResultLevel = table.Column<string>(type: "text", nullable: true),
                    TotalQuestions = table.Column<int>(type: "integer", nullable: false),
                    CorrectAnswers = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlacementTestSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlacementTestSessions_UserLanguageProfiles_LanguageProfileId",
                        column: x => x.LanguageProfileId,
                        principalTable: "UserLanguageProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlacementTestSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlacementTestQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlacementTestSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Skill = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Prompt = table.Column<string>(type: "text", nullable: false),
                    Options = table.Column<string>(type: "text", nullable: false),
                    CorrectAnswer = table.Column<string>(type: "text", nullable: false),
                    Explanation = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlacementTestQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlacementTestQuestions_PlacementTestSessions_PlacementTestS~",
                        column: x => x.PlacementTestSessionId,
                        principalTable: "PlacementTestSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlacementTestAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlacementTestSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlacementTestQuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserAnswer = table.Column<string>(type: "text", nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    AnsweredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlacementTestAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlacementTestAnswers_PlacementTestQuestions_PlacementTestQu~",
                        column: x => x.PlacementTestQuestionId,
                        principalTable: "PlacementTestQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlacementTestAnswers_PlacementTestSessions_PlacementTestSes~",
                        column: x => x.PlacementTestSessionId,
                        principalTable: "PlacementTestSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlacementTestAnswers_PlacementTestQuestionId",
                table: "PlacementTestAnswers",
                column: "PlacementTestQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_PlacementTestAnswers_PlacementTestSessionId_AnsweredAt",
                table: "PlacementTestAnswers",
                columns: new[] { "PlacementTestSessionId", "AnsweredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PlacementTestQuestions_PlacementTestSessionId_Order",
                table: "PlacementTestQuestions",
                columns: new[] { "PlacementTestSessionId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlacementTestSessions_LanguageProfileId_Status",
                table: "PlacementTestSessions",
                columns: new[] { "LanguageProfileId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PlacementTestSessions_UserId_StartedAt",
                table: "PlacementTestSessions",
                columns: new[] { "UserId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserLanguageProfiles_UserId_LanguageCode",
                table: "UserLanguageProfiles",
                columns: new[] { "UserId", "LanguageCode" },
                unique: true);

            // Перенос языковых настроек существующих пользователей.
            migrationBuilder.Sql("""
    INSERT INTO "UserLanguageProfiles"
    (
        "Id",
        "UserId",
        "LanguageCode",
        "SelectedLevel",
        "CurrentLevel",
        "IsPlacementCompleted",
        "CreatedAt",
        "LastTestedAt"
    )
    SELECT
        gen_random_uuid(),
        u."Id",
        COALESCE(NULLIF(s."LearningLanguage", ''), 'en'),
        COALESCE(NULLIF(s."Level", ''), 'A1-A2'),
        NULL,
        FALSE,
        NOW(),
        NULL
    FROM "Users" u
    LEFT JOIN "UserSettings" s
        ON s."UserId" = u."Id";

    -- Перенос языка и уровня в существующие диалоги.
    UPDATE "DialogueSessions" d
    SET
        "LanguageCode" = COALESCE(
            NULLIF(s."LearningLanguage", ''),
            'en'
        ),
        "Level" = COALESCE(
            NULLIF(s."Level", ''),
            'A1-A2'
        )
    FROM "Users" u
    LEFT JOIN "UserSettings" s
        ON s."UserId" = u."Id"
    WHERE d."UserId" = u."Id";
    """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlacementTestAnswers");

            migrationBuilder.DropTable(
                name: "PlacementTestQuestions");

            migrationBuilder.DropTable(
                name: "PlacementTestSessions");

            migrationBuilder.DropTable(
                name: "UserLanguageProfiles");

            migrationBuilder.DropColumn(
                name: "LanguageCode",
                table: "DialogueSessions");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "DialogueSessions");
        }
    }
}
