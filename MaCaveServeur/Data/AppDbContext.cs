using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using MaCaveServeur.Models;

namespace MaCaveServeur.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Bottle> Bottles => Set<Bottle>();
        public DbSet<Supplier> Suppliers => Set<Supplier>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Bottles
            modelBuilder.Entity<Bottle>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Name).HasMaxLength(256);
                e.Property(x => x.Producer).HasMaxLength(256);
                e.Property(x => x.Appellation).HasMaxLength(256);
                e.Property(x => x.Region).HasMaxLength(256);
                e.Property(x => x.Country).HasMaxLength(64);
                e.Property(x => x.Supplier).HasMaxLength(256);
                e.Property(x => x.Color).HasMaxLength(64);
                e.Property(x => x.Location).HasMaxLength(128);

                // Liste de tags RFID sérialisée en JSON dans une colonne texte (facultatif si RFID endormi)
                e.Property<string?>("RfidTagsSerialized");
                e.Ignore(b => b.RfidTags); // évite double mapping si tu ne l’utilises pas maintenant
            });

            // Suppliers
            modelBuilder.Entity<Supplier>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Name).HasMaxLength(256).IsRequired();
                e.Property(x => x.OrderEmail).HasMaxLength(256);
                e.Property(x => x.ContactName).HasMaxLength(256);
                e.Property(x => x.Phone).HasMaxLength(64);
                e.Property(x => x.Notes).HasMaxLength(2048);
                e.HasIndex(x => x.Name).IsUnique(false);
            });
        }
    }
}
