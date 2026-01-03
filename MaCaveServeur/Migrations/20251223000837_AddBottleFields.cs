using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaCaveServeur.Migrations
{
    /// <inheritdoc />
    public partial class AddBottleFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Capacity",
                table: "Bottles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Designation",
                table: "Bottles",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Bottles",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GlassExternalId",
                table: "Bottles",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GlassSellPrice",
                table: "Bottles",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ProductReferenceCode",
                table: "Bottles",
                type: "TEXT",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Capacity",
                table: "Bottles");

            migrationBuilder.DropColumn(
                name: "Designation",
                table: "Bottles");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Bottles");

            migrationBuilder.DropColumn(
                name: "GlassExternalId",
                table: "Bottles");

            migrationBuilder.DropColumn(
                name: "GlassSellPrice",
                table: "Bottles");

            migrationBuilder.DropColumn(
                name: "ProductReferenceCode",
                table: "Bottles");
        }
    }
}
