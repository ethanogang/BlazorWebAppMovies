using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaCaveServeur.Migrations
{
    public partial class InitCleanModel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IMPORTANT POUR SQLITE :
            // désactiver les FK hors transaction
            migrationBuilder.Sql(
                "PRAGMA foreign_keys = 0;",
                suppressTransaction: true
            );

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
                    Region = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Grapes = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Supplier = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Vintage = table.Column<int>(type: "INTEGER", nullable: false),
                    PurchasePrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    SalePrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    LowStockThreshold = table.Column<int>(type: "INTEGER", nullable: false),
                    SiteCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    RfidTagsSerialized = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    LastSeenUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bottles", x => x.Id);
                });

            // Réactiver les FK hors transaction
            migrationBuilder.Sql(
                "PRAGMA foreign_keys = 1;",
                suppressTransaction: true
            );
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "PRAGMA foreign_keys = 0;",
                suppressTransaction: true
            );

            migrationBuilder.DropTable(
                name: "Bottles");

            migrationBuilder.Sql(
                "PRAGMA foreign_keys = 1;",
                suppressTransaction: true
            );
        }
    }
}
