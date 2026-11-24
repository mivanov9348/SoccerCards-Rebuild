using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DugoutDeck.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CardTiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CareerCards_CareerId_Code",
                table: "CareerCards");

            migrationBuilder.AddColumn<string>(
                name: "DraftJson",
                table: "Careers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Tier",
                table: "CareerCards",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_CareerCards_CareerId_Code_Tier",
                table: "CareerCards",
                columns: new[] { "CareerId", "Code", "Tier" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CareerCards_CareerId_Code_Tier",
                table: "CareerCards");

            migrationBuilder.DropColumn(
                name: "DraftJson",
                table: "Careers");

            migrationBuilder.DropColumn(
                name: "Tier",
                table: "CareerCards");

            migrationBuilder.CreateIndex(
                name: "IX_CareerCards_CareerId_Code",
                table: "CareerCards",
                columns: new[] { "CareerId", "Code" },
                unique: true);
        }
    }
}
