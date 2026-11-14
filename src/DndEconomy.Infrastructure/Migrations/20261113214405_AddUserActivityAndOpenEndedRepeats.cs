using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DndEconomy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserActivityAndOpenEndedRepeats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "RepeatUntil",
                table: "AvailabilityRule",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: true);
            // Существующие конечные серии сохраняют прежнюю последнюю дату.
            migrationBuilder.Sql("UPDATE \"AvailabilityRule\" SET \"RepeatUntil\" = \"LocalEnd\"::date WHERE \"Repeat\" <> 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RepeatUntil",
                table: "AvailabilityRule");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "AspNetUsers");
        }
    }
}
