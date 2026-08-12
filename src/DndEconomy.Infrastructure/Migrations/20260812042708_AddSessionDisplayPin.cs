using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DndEconomy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionDisplayPin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPinnedForDisplay",
                table: "EconomySessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_EconomySessions_IsPinnedForDisplay",
                table: "EconomySessions",
                column: "IsPinnedForDisplay",
                unique: true,
                filter: "\"IsPinnedForDisplay\" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EconomySessions_IsPinnedForDisplay",
                table: "EconomySessions");

            migrationBuilder.DropColumn(
                name: "IsPinnedForDisplay",
                table: "EconomySessions");
        }
    }
}
