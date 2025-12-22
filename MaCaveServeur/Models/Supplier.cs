using System;

namespace MaCaveServeur.Models
{
    public class Supplier
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;   // Nom du fournisseur
        public string? ContactName { get; set; }           // Nom du commercial
        public string? OrderEmail { get; set; }            // Email pour commandes
        public string? Phone { get; set; }
        public string? Notes { get; set; }
    }
}
