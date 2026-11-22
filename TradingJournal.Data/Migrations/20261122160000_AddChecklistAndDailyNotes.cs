using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Data.Migrations
{
    // Strateji kontrol listesi (StrategyRules + TradeRuleChecks) ve günlük notlar (DailyNotes).
    [DbContext(typeof(TradingJournalDbContext))]
    [Migration("20261122160000_AddChecklistAndDailyNotes")]
    public partial class AddChecklistAndDailyNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Plan = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    Review = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    Mood = table.Column<int>(type: "INTEGER", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyNotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StrategyRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StrategyId = table.Column<int>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategyRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StrategyRules_Strategies_StrategyId",
                        column: x => x.StrategyId,
                        principalTable: "Strategies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TradeRuleChecks",
                columns: table => new
                {
                    TradeId = table.Column<int>(type: "INTEGER", nullable: false),
                    StrategyRuleId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsMet = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeRuleChecks", x => new { x.TradeId, x.StrategyRuleId });
                    table.ForeignKey(
                        name: "FK_TradeRuleChecks_StrategyRules_StrategyRuleId",
                        column: x => x.StrategyRuleId,
                        principalTable: "StrategyRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TradeRuleChecks_Trades_TradeId",
                        column: x => x.TradeId,
                        principalTable: "Trades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyNotes_Date",
                table: "DailyNotes",
                column: "Date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StrategyRules_StrategyId",
                table: "StrategyRules",
                column: "StrategyId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeRuleChecks_StrategyRuleId",
                table: "TradeRuleChecks",
                column: "StrategyRuleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "DailyNotes");
            migrationBuilder.DropTable(name: "TradeRuleChecks");
            migrationBuilder.DropTable(name: "StrategyRules");
        }
    }
}
