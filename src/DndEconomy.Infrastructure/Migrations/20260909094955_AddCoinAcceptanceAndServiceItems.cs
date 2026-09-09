using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DndEconomy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCoinAcceptanceAndServiceItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsService",
                table: "Items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CoinAcceptanceNote",
                table: "Cities",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CityCoinAcceptances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Denomination = table.Column<int>(type: "integer", nullable: false),
                    CityId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcceptanceRate = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CityCoinAcceptances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CityCoinAcceptances_Cities_CityId",
                        column: x => x.CityId,
                        principalTable: "Cities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CityCoinAcceptances_CityId_Denomination",
                table: "CityCoinAcceptances",
                columns: new[] { "CityId", "Denomination" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CityCoinAcceptances");

            migrationBuilder.DropColumn(
                name: "IsService",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "CoinAcceptanceNote",
                table: "Cities");
        }
    }
}
