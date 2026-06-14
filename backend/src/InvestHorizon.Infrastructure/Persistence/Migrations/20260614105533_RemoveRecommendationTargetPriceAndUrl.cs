using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvestHorizon.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRecommendationTargetPriceAndUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetPrice",
                table: "Recommendations");

            migrationBuilder.DropColumn(
                name: "Url",
                table: "Recommendations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TargetPrice",
                table: "Recommendations",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Url",
                table: "Recommendations",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }
    }
}
