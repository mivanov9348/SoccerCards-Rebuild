using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DugoutDeck.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Release1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Teams",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EditedInGame",
                table: "Teams",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Leagues",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Difficulty",
                table: "Careers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Teams_Code",
                table: "Teams",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Leagues_Code",
                table: "Leagues",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Teams_Code",
                table: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_Leagues_Code",
                table: "Leagues");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "EditedInGame",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Leagues");

            migrationBuilder.DropColumn(
                name: "Difficulty",
                table: "Careers");
        }
    }
}
