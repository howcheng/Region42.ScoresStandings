using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Region42.ScoresStandings.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomMessageToSeasonAndDivision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomMessage",
                table: "Seasons",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomMessage",
                table: "Divisions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomMessage",
                table: "Seasons");

            migrationBuilder.DropColumn(
                name: "CustomMessage",
                table: "Divisions");
        }
    }
}
