using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaCaveServeur.Migrations
{
    /// <inheritdoc />
    public partial class AddBottlesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bottles_Location_Supplier_Name_Vintage",
                table: "Bottles");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Name",
                table: "Suppliers",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Suppliers_Name",
                table: "Suppliers");

            migrationBuilder.CreateIndex(
                name: "IX_Bottles_Location_Supplier_Name_Vintage",
                table: "Bottles",
                columns: new[] { "Location", "Supplier", "Name", "Vintage" });
        }
    }
}
