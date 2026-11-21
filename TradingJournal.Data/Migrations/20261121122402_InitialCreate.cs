using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TradingJournal.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Broker = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    InitialBalance = table.Column<decimal>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Instruments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    PipSize = table.Column<decimal>(type: "TEXT", nullable: false),
                    PipValuePerLot = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Instruments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Strategies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Strategies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    ColorHex = table.Column<string>(type: "TEXT", maxLength: 7, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AccountRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    RuleType = table.Column<int>(type: "INTEGER", nullable: false),
                    LimitPercent = table.Column<decimal>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountRules_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Trades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    InstrumentId = table.Column<int>(type: "INTEGER", nullable: false),
                    StrategyId = table.Column<int>(type: "INTEGER", nullable: true),
                    Direction = table.Column<int>(type: "INTEGER", nullable: false),
                    OpenTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CloseTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    EntryPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    StopLoss = table.Column<decimal>(type: "TEXT", nullable: false),
                    TakeProfit = table.Column<decimal>(type: "TEXT", nullable: true),
                    ExitPrice = table.Column<decimal>(type: "TEXT", nullable: true),
                    Commission = table.Column<decimal>(type: "TEXT", nullable: false),
                    Rating = table.Column<int>(type: "INTEGER", nullable: true),
                    TradeType = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: true),
                    Leverage = table.Column<int>(type: "INTEGER", nullable: true),
                    LotSize = table.Column<decimal>(type: "TEXT", nullable: true),
                    PipSize = table.Column<decimal>(type: "TEXT", nullable: true),
                    PipValuePerLot = table.Column<decimal>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Trades_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Trades_Instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Trades_Strategies_StrategyId",
                        column: x => x.StrategyId,
                        principalTable: "Strategies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TradeScreenshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TradeId = table.Column<int>(type: "INTEGER", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Caption = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeScreenshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradeScreenshots_Trades_TradeId",
                        column: x => x.TradeId,
                        principalTable: "Trades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TradeTags",
                columns: table => new
                {
                    TagsId = table.Column<int>(type: "INTEGER", nullable: false),
                    TradesId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeTags", x => new { x.TagsId, x.TradesId });
                    table.ForeignKey(
                        name: "FK_TradeTags_Tags_TagsId",
                        column: x => x.TagsId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TradeTags_Trades_TradesId",
                        column: x => x.TradesId,
                        principalTable: "Trades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Instruments",
                columns: new[] { "Id", "PipSize", "PipValuePerLot", "Symbol", "Type" },
                values: new object[,]
                {
                    { 1, 0.0001m, 10m, "EURUSD", 1 },
                    { 2, 0.0001m, 10m, "GBPUSD", 1 },
                    { 3, 0.01m, 6.5m, "USDJPY", 1 },
                    { 4, 0.1m, 10m, "XAUUSD", 1 },
                    { 5, 0.01m, 1m, "BTCUSDT", 2 },
                    { 6, 0.01m, 1m, "ETHUSDT", 2 }
                });

            migrationBuilder.InsertData(
                table: "Tags",
                columns: new[] { "Id", "ColorHex", "Name" },
                values: new object[,]
                {
                    { 1, "#2E7D32", "A+ Setup" },
                    { 2, "#C62828", "FOMO" },
                    { 3, "#AD1457", "Revenge" },
                    { 4, "#F9A825", "Haber Günü" },
                    { 5, "#6A1B9A", "Erken Çıkış" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountRules_AccountId_RuleType",
                table: "AccountRules",
                columns: new[] { "AccountId", "RuleType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Instruments_Symbol",
                table: "Instruments",
                column: "Symbol",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Strategies_Name",
                table: "Strategies",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tags_Name",
                table: "Tags",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trades_AccountId",
                table: "Trades",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Trades_InstrumentId",
                table: "Trades",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Trades_OpenTime",
                table: "Trades",
                column: "OpenTime");

            migrationBuilder.CreateIndex(
                name: "IX_Trades_StrategyId",
                table: "Trades",
                column: "StrategyId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeScreenshots_TradeId",
                table: "TradeScreenshots",
                column: "TradeId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeTags_TradesId",
                table: "TradeTags",
                column: "TradesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountRules");

            migrationBuilder.DropTable(
                name: "TradeScreenshots");

            migrationBuilder.DropTable(
                name: "TradeTags");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropTable(
                name: "Trades");

            migrationBuilder.DropTable(
                name: "Accounts");

            migrationBuilder.DropTable(
                name: "Instruments");

            migrationBuilder.DropTable(
                name: "Strategies");
        }
    }
}
