using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaCaveServeur.Models
{
    public class Bottle
    {
        [Key]
        public Guid Id { get; set; }

        // --- Champs "nouveaux" (cible) ---
        [MaxLength(256)] public string? Color { get; set; }          // ex: RED-WINE / WHITE-WINE / ...
        [MaxLength(256)] public string? Name { get; set; }           // nom de la cuvée
        [MaxLength(256)] public string? Appellation { get; set; }
        [MaxLength(256)] public string? Producer { get; set; }       // Domaine / Château
        [MaxLength(128)] public string? Country { get; set; }
        [MaxLength(256)] public string? Region { get; set; }
        [MaxLength(256)] public string? Grapes { get; set; }         // cépages
        [MaxLength(256)] public string? Supplier { get; set; }
        public int Vintage { get; set; }                             // millésime
        public decimal PurchasePrice { get; set; }
        public decimal SalePrice { get; set; }

        public int Quantity { get; set; }
        public int LowStockThreshold { get; set; }

        // Site (codes fixes)
        [MaxLength(64)] public string? SiteCode { get; set; }

        [MaxLength(2000)] public string? Notes { get; set; }

        // --- RFID (optionnel) ---
        [MaxLength(4000)]
        public string? RfidTagsSerialized { get; set; }

        public DateTime? LastSeenUtc { get; set; }

        [NotMapped]
        public List<string>? RfidTags
        {
            get
            {
                if (_rfidTags != null) return _rfidTags;
                if (string.IsNullOrWhiteSpace(RfidTagsSerialized)) return _emptyList;

                _rfidTags = RfidTagsSerialized.Split('|', StringSplitOptions.RemoveEmptyEntries)
                                              .Select(t => t.Trim())
                                              .Where(t => t.Length > 0)
                                              .ToList();
                return _rfidTags;
            }
            set
            {
                _rfidTags = value ?? new List<string>();
                RfidTagsSerialized = string.Join("|", _rfidTags);
            }
        }

        private List<string>? _rfidTags;
        private static readonly List<string> _emptyList = new();

        // ------------------------------------------------------------------
        //          ALIAS POUR COMPATIBILITÉ AVEC LES ANCIENNES PAGES
        // ------------------------------------------------------------------

        /// <summary>Ancien champ: Type (on le mappe sur Color).</summary>
        [NotMapped]
        public string? Type
        {
            get => Color;
            set => Color = value;
        }

        /// <summary>Ancien champ: Year (on le mappe sur Vintage).</summary>
        [NotMapped]
        public int? Year
        {
            get => Vintage == 0 ? null : Vintage;
            set => Vintage = value ?? 0;
        }

        /// <summary>Ancien champ: Grape (on le mappe sur Grapes).</summary>
        [NotMapped]
        public string? Grape
        {
            get => Grapes;
            set => Grapes = value;
        }

        /// <summary>Ancien champ: Location (on le mappe sur SiteCode).</summary>
        [NotMapped]
        public string? Location
        {
            get => SiteCode;
            set => SiteCode = value;
        }
    }
}
