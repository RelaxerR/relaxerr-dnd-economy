using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DndEconomy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HardenScheduledGamesAndPushSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SecurityStamp",
                table: "ChatPushSubscription",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EconomySession_CancelledNotPinned",
                table: "EconomySessions",
                sql: "NOT (\"IsCancelled\" AND \"IsPinnedForDisplay\")");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_EconomySession_CancelledNotPinned",
                table: "EconomySessions");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "ChatPushSubscription");
        }
    }
}
