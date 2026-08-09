using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Region42.ScoresStandings.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddIsRegion42TeamToTeam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRegion42Team",
                table: "Teams",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRegion42Team",
                table: "Teams");
        }
    }
}
