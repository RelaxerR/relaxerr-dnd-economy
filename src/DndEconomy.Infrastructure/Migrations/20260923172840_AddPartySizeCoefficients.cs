using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DndEconomy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPartySizeCoefficients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PartySizeCoefficients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartySize = table.Column<int>(type: "integer", nullable: false),
                    Coefficient = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartySizeCoefficients", x => x.Id);
                    table.CheckConstraint("CK_PartySizeCoefficients_CoefficientPositive", "\"Coefficient\" > 0");
                    table.CheckConstraint("CK_PartySizeCoefficients_PartySizeRange", "\"PartySize\" >= 1 AND \"PartySize\" <= 8");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PartySizeCoefficients_PartySize",
                table: "PartySizeCoefficients",
                column: "PartySize",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartySizeCoefficients");
        }
    }
}
