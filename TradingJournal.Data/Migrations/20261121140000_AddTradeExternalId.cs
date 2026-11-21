using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Data.Migrations
{
    // Trades tablosuna dış sistem kimliği sütunu (Chart Vault içe aktarımı için).
    [DbContext(typeof(TradingJournalDbContext))]
    [Migration("20261121140000_AddTradeExternalId")]
    public partial class AddTradeExternalId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Trades",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trades_ExternalId",
                table: "Trades",
                column: "ExternalId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Trades_ExternalId",
                table: "Trades");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Trades");
        }
    }
}
