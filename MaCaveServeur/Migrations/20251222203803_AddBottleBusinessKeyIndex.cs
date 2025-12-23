using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaCaveServeur.Migrations
{
    /// <inheritdoc />
    public partial class AddBottleBusinessKeyIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Bottles_SiteCode_Producer_Name_Vintage_Supplier",
                table: "Bottles",
                columns: new[] { "SiteCode", "Producer", "Name", "Vintage", "Supplier" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bottles_SiteCode_Producer_Name_Vintage_Supplier",
                table: "Bottles");
        }
    }
}
