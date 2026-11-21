using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication1.Migrations
{
    /// <inheritdoc />
    public partial class AddDialogueAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DialogueMessages_SessionId",
                table: "DialogueMessages");

            migrationBuilder.CreateTable(
                name: "DetectedErrors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalText = table.Column<string>(type: "text", nullable: false),
                    CorrectedText = table.Column<string>(type: "text", nullable: false),
                    Explanation = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetectedErrors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DetectedErrors_DialogueSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "DialogueSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DialogueAnalyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OverallFeedback = table.Column<string>(type: "text", nullable: false),
                    Strengths = table.Column<string>(type: "text", nullable: false),
                    RecommendedLevel = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DialogueAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DialogueAnalyses_DialogueSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "DialogueSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DialogueMessages_SessionId_CreatedAt",
                table: "DialogueMessages",
                columns: new[] { "SessionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DetectedErrors_SessionId_Category",
                table: "DetectedErrors",
                columns: new[] { "SessionId", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_DialogueAnalyses_SessionId",
                table: "DialogueAnalyses",
                column: "SessionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DetectedErrors");

            migrationBuilder.DropTable(
                name: "DialogueAnalyses");

            migrationBuilder.DropIndex(
                name: "IX_DialogueMessages_SessionId_CreatedAt",
                table: "DialogueMessages");

            migrationBuilder.CreateIndex(
                name: "IX_DialogueMessages_SessionId",
                table: "DialogueMessages",
                column: "SessionId");
        }
    }
}
