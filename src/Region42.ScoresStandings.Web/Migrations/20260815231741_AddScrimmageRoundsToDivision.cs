using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Region42.ScoresStandings.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddScrimmageRoundsToDivision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ScrimmageRounds",
                table: "Divisions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScrimmageRounds",
                table: "Divisions");
        }
    }
}
