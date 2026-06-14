using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestHorizon.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceAndFxHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FxRateHistory",
                columns: table => new
                {
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    RatePerEur = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FxRateHistory", x => new { x.Currency, x.Date });
                });

            migrationBuilder.CreateTable(
                name: "InstrumentPriceHistory",
                columns: table => new
                {
                    InstrumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    CloseNative = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstrumentPriceHistory", x => new { x.InstrumentId, x.Date });
                    table.ForeignKey(
                        name: "FK_InstrumentPriceHistory_Instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FxRateHistory");

            migrationBuilder.DropTable(
                name: "InstrumentPriceHistory");
        }
    }
}
