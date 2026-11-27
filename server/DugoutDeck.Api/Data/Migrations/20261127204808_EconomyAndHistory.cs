using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DugoutDeck.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class EconomyAndHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Expenses",
                table: "CareerSeasons",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Income",
                table: "CareerSeasons",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "AchievementsJson",
                table: "Careers",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Expenses",
                table: "CareerSeasons");

            migrationBuilder.DropColumn(
                name: "Income",
                table: "CareerSeasons");

            migrationBuilder.DropColumn(
                name: "AchievementsJson",
                table: "Careers");
        }
    }
}
