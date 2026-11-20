using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication1.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageReplies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DialogueMessages_SessionId_CreatedAt",
                table: "DialogueMessages");

            migrationBuilder.AddColumn<Guid>(
                name: "ReplyToMessageId",
                table: "DialogueMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DialogueMessages_ReplyToMessageId",
                table: "DialogueMessages",
                column: "ReplyToMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_DialogueMessages_SessionId",
                table: "DialogueMessages",
                column: "SessionId");

            migrationBuilder.AddForeignKey(
                name: "FK_DialogueMessages_DialogueMessages_ReplyToMessageId",
                table: "DialogueMessages",
                column: "ReplyToMessageId",
                principalTable: "DialogueMessages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DialogueMessages_DialogueMessages_ReplyToMessageId",
                table: "DialogueMessages");

            migrationBuilder.DropIndex(
                name: "IX_DialogueMessages_ReplyToMessageId",
                table: "DialogueMessages");

            migrationBuilder.DropIndex(
                name: "IX_DialogueMessages_SessionId",
                table: "DialogueMessages");

            migrationBuilder.DropColumn(
                name: "ReplyToMessageId",
                table: "DialogueMessages");

            migrationBuilder.CreateIndex(
                name: "IX_DialogueMessages_SessionId_CreatedAt",
                table: "DialogueMessages",
                columns: new[] { "SessionId", "CreatedAt" });
        }
    }
}
