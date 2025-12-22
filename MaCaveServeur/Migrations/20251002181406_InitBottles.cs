using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaCaveServeur.Migrations
{
    /// <inheritdoc />
    public partial class InitBottles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Bottles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Appellation = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Producer = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Country = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Region = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Supplier = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Grapes = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Vintage = table.Column<int>(type: "INTEGER", nullable: false),
                    PurchasePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SalePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    LowStockThreshold = table.Column<int>(type: "INTEGER", nullable: false),
                    Location = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    RfidTagsSerialized = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    LastSeenUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bottles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bottles_Location_Supplier_Name_Vintage",
                table: "Bottles",
                columns: new[] { "Location", "Supplier", "Name", "Vintage" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bottles");
        }
    }
}
