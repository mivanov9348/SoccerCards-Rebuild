using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DugoutDeck.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class TacticPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TacticPlansJson",
                table: "Careers",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TacticPlansJson",
                table: "Careers");
        }
    }
}
