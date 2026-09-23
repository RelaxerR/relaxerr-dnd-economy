using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DndEconomy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceQuestPayRatesWithEconomyActivities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuestPayRates");

            migrationBuilder.CreateTable(
                name: "EconomyActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityType = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DangerLevel = table.Column<int>(type: "integer", nullable: true),
                    MinLevel = table.Column<int>(type: "integer", nullable: false),
                    MaxLevel = table.Column<int>(type: "integer", nullable: false),
                    RateMin = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RateMax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RecommendedDurationDays = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    BalanceNote = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EconomyActivities", x => x.Id);
                    table.CheckConstraint("CK_EconomyActivities_DangerLevelOnlyForQuest", "(\"ActivityType\" = 0) = (\"DangerLevel\" IS NOT NULL)");
                    table.CheckConstraint("CK_EconomyActivities_DurationPositive", "\"RecommendedDurationDays\" >= 1");
                    table.CheckConstraint("CK_EconomyActivities_LevelRange", "\"MinLevel\" >= 1 AND \"MinLevel\" <= \"MaxLevel\" AND \"MaxLevel\" <= 20");
                    table.CheckConstraint("CK_EconomyActivities_RateRange", "\"RateMin\" <= \"RateMax\"");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EconomyActivities_ActivityType_MinLevel_Category",
                table: "EconomyActivities",
                columns: new[] { "ActivityType", "MinLevel", "Category" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EconomyActivities");

            migrationBuilder.CreateTable(
                name: "QuestPayRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BalanceNote = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DangerLevel = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Duration = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Epoch = table.Column<int>(type: "integer", nullable: false),
                    PartyPayment = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestPayRates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuestPayRates_Epoch_Category_DangerLevel",
                table: "QuestPayRates",
                columns: new[] { "Epoch", "Category", "DangerLevel" });
        }
    }
}
