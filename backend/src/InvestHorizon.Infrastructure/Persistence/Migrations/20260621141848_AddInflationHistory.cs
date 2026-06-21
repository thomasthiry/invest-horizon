using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestHorizon.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInflationHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InflationHistory",
                columns: table => new
                {
                    Region = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Index = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InflationHistory", x => new { x.Region, x.Date });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InflationHistory");
        }
    }
}
