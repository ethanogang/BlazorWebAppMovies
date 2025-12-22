using MaCaveServeur.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MaCaveServeur.Data
{
    // Permet à 'dotnet ef' d'instancier AppDbContext sans lancer l'appli
    public class DesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                // même chaîne que dans appsettings.json
                .UseSqlite("Data Source=Data/cave.db")
                .Options;

            return new AppDbContext(options);
        }
    }
}
