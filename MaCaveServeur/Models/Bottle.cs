using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaCaveServeur.Models
{
    public class Bottle
    {
        [Key]
        public Guid Id { get; set; }

        // Métadonnées produit
        [MaxLength(256)] public string? Color { get; set; }          // RED-WINE, WHITE-WINE, MOELLEUX, etc.
        [MaxLength(256)] public string? Name { get; set; }           // nom de la cuvée / bouteille
        [MaxLength(256)] public string? Appellation { get; set; }
        [MaxLength(256)] public string? Producer { get; set; }       // Domaine / Château
        [MaxLength(128)] public string? Country { get; set; }        // FR, IT, ES...
        [MaxLength(128)] public string? Region { get; set; }         // Bourgogne, Rhône, etc.
        [MaxLength(256)] public string? Supplier { get; set; }       // Fournisseur (pour commandes)
        [MaxLength(256)] public string? Grapes { get; set; }         // cépages
        public int Vintage { get; set; }                             // millésime

        // Prix & stock
        [Column(TypeName = "decimal(18,2)")] public decimal PurchasePrice { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal SalePrice { get; set; }
        public int Quantity { get; set; }
        public int LowStockThreshold { get; set; } = 1;

        // Localisation (établissement / cave)
        [MaxLength(64)] public string? Location { get; set; }        // BRUTUS / MAILLARD / MERLOT / BACCHUS / GRAMMA / BISTROT_MAURICE / ALL

        // Notes libres (utilisé par les pages New/Edit)
        [MaxLength(2000)] public string? Notes { get; set; }

        // --- RFID (dormant) ---
        [NotMapped]
        public List<string>? RfidTags
        {
            get
            {
                if (_rfidTags != null) return _rfidTags;
                if (string.IsNullOrWhiteSpace(RfidTagsSerialized)) return _emptyList;
                _rfidTags = RfidTagsSerialized.Split('|', StringSplitOptions.RemoveEmptyEntries)
                                              .Select(t => t.Trim()).Where(t => t.Length > 0).ToList();
                return _rfidTags;
            }
            set
            {
                _rfidTags = value ?? new List<string>();
                RfidTagsSerialized = string.Join("|", _rfidTags);
            }
        }

        [MaxLength(4000)]
        public string? RfidTagsSerialized { get; set; }

        public DateTime? LastSeenUtc { get; set; }

        private List<string>? _rfidTags;
        private static readonly List<string> _emptyList = new();
    }
}
