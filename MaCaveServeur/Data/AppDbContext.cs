using Microsoft.EntityFrameworkCore;
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

                e.Property(x => x.SiteCode).HasMaxLength(64);

                e.Property(x => x.Notes).HasMaxLength(2000);
                e.Property(x => x.RfidTagsSerialized).HasMaxLength(4000);

                // Index “anti-doublons” (non-unique pour éviter blocage si DB contient déjà des doublons)
                e.HasIndex(x => new { x.SiteCode, x.Producer, x.Name, x.Vintage, x.Supplier });
            });

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
