using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using MaCaveServeur.Data;
using MaCaveServeur.Models;

namespace MaCaveServeur.Services
{
    public class CellarService
    {
        private readonly AppDbContext _db;

        public CellarService(AppDbContext db)
        {
            _db = db;
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        // =======================
        //   LECTURE / ÉCRITURE
        // =======================

        // Compatibilité pages existantes
        public IQueryable<Bottle> Bottles => _db.Bottles;

        /// <summary>Retourne toutes les bouteilles (no tracking) pour affichage/dashboards.</summary>
        public List<Bottle> GetAll() => _db.Bottles.AsNoTracking().ToList();

        /// <summary>Alias IEnumerable si utilisé quelque part.</summary>
        public IEnumerable<Bottle> All() => _db.Bottles.AsNoTracking().AsEnumerable();

        public Bottle? Get(Guid id) => _db.Bottles.Find(id);

        public void Add(Bottle b)
        {
            if (b.Id == Guid.Empty) b.Id = Guid.NewGuid();
            _db.Bottles.Add(b);
            _db.SaveChanges();
        }

        public void Upsert(Bottle b)
        {
            var existing = _db.Bottles.FirstOrDefault(x => x.Id == b.Id);
            if (existing == null)
            {
                Add(b);
                return;
            }

            existing.Color = b.Color;
            existing.Name = b.Name;
            existing.Appellation = b.Appellation;
            existing.Producer = b.Producer;
            existing.Country = b.Country;
            existing.Region = b.Region;
            existing.Supplier = b.Supplier;
            existing.Grapes = b.Grapes;
            existing.Vintage = b.Vintage;
            existing.PurchasePrice = b.PurchasePrice;
            existing.SalePrice = b.SalePrice;
            existing.Quantity = b.Quantity;
            existing.LowStockThreshold = b.LowStockThreshold;
            existing.Location = b.Location;

            _db.SaveChanges();
        }

        public void Delete(Guid id)
        {
            var e = _db.Bottles.Find(id);
            if (e != null)
            {
                _db.Bottles.Remove(e);
                _db.SaveChanges();
            }
        }

        /// <summary>Supprime toutes les bouteilles (utilisé par une route /purge éventuelle).</summary>
        public int DeleteAll()
        {
            var all = _db.Bottles.ToList();
            _db.Bottles.RemoveRange(all);
            _db.SaveChanges();
            return all.Count;
        }

        // ====================
        //   IMPORT / EXPORT
        // ====================

        /// <summary>
        /// Importe un fichier Excel selon ton mapping (en-têtes exacts) :
        /// COULEUR | NOM | APPELATION | PRODUCTEUR | PAYS | REGION | FOURNISSEUR | MILLESIME | CEPAGES | PRIX ACHAT | PRIX VENTE | SEUIL ALERTE | QUANTITE
        /// Le site d’affectation est passé en paramètre (Location).
        /// </summary>
        public int ImportExcel(Stream excelStream, string siteCode)
        {
            using var p = new ExcelPackage(excelStream);
            var ws = p.Workbook.Worksheets[0];

            // Lecture de l’en-tête (ligne 1)
            var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int col = 1;
            while (!string.IsNullOrWhiteSpace(ws.Cells[1, col].Text))
            {
                headers[ws.Cells[1, col].Text.Trim()] = col;
                col++;
            }

            // Helper pour obtenir la valeur d'une colonne par nom
            string T(int row, string key) =>
                headers.TryGetValue(key, out var c) ? (ws.Cells[row, c].Text ?? "").Trim() : "";

            int imported = 0;
            int row = 2;

            while (true)
            {
                var rawName = T(row, "NOM");
                var rawProducer = T(row, "PRODUCTEUR");
                var rawApp = T(row, "APPELATION"); // orthographe telle que fournie
                var rawEmptyCheck = string.Concat(rawName, rawProducer, rawApp).Trim();

                if (string.IsNullOrWhiteSpace(rawEmptyCheck))
                    break; // fin des données

                var b = new Bottle
                {
                    Id = Guid.NewGuid(),
                    Color = T(row, "COULEUR"),
                    Name = rawName,
                    Appellation = rawApp,
                    Producer = rawProducer,
                    Country = T(row, "PAYS"),
                    Region = T(row, "REGION"),
                    Supplier = T(row, "FOURNISSEUR"),
                    Grapes = T(row, "CEPAGES"),
                    Vintage = ParseIntSafe(T(row, "MILLESIME")),
                    PurchasePrice = ParseMoney(T(row, "PRIX ACHAT")),
                    SalePrice = ParseMoney(T(row, "PRIX VENTE")),
                    LowStockThreshold = ParseIntSafe(T(row, "SEUIL ALERTE"), 1),
                    Quantity = ParseIntSafe(T(row, "QUANTITE"), 0),
                    Location = siteCode?.Trim().ToUpperInvariant() ?? "ALL"
                };

                _db.Bottles.Add(b);
                imported++;
                row++;
            }

            _db.SaveChanges();
            return imported;
        }

        /// <summary>
        /// Exporte l’ensemble des bouteilles en Excel avec les mêmes colonnes que l’import (sans RFID/EPC).
        /// </summary>
        public byte[] ExportExcel()
        {
            using var p = new ExcelPackage();
            var ws = p.Workbook.Worksheets.Add("Cave");

            // En-têtes
            string[] H = {
                "COULEUR","NOM","APPELATION","PRODUCTEUR","PAYS","REGION",
                "FOURNISSEUR","MILLESIME","CEPAGES","PRIX ACHAT","PRIX VENTE",
                "SEUIL ALERTE","QUANTITE","SITE"
            };
            for (int i = 0; i < H.Length; i++)
                ws.Cells[1, i + 1].Value = H[i];

            // Lignes
            int r = 2;
            foreach (var b in _db.Bottles.AsNoTracking())
            {
                ws.Cells[r, 1].Value = b.Color;
                ws.Cells[r, 2].Value = b.Name;
                ws.Cells[r, 3].Value = b.Appellation;
                ws.Cells[r, 4].Value = b.Producer;
                ws.Cells[r, 5].Value = b.Country;
                ws.Cells[r, 6].Value = b.Region;
                ws.Cells[r, 7].Value = b.Supplier;
                ws.Cells[r, 8].Value = b.Vintage;
                ws.Cells[r, 9].Value = b.Grapes;
                ws.Cells[r,10].Value = b.PurchasePrice;
                ws.Cells[r,11].Value = b.SalePrice;
                ws.Cells[r,12].Value = b.LowStockThreshold;
                ws.Cells[r,13].Value = b.Quantity;
                ws.Cells[r,14].Value = b.Location;
                r++;
            }

            // Style simple
            using (var rng = ws.Cells[1, 1, 1, H.Length])
            {
                rng.Style.Font.Bold = true;
                rng.Style.Fill.PatternType = ExcelFillStyle.Solid;
                rng.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(28, 22, 48));
                rng.Style.Font.Color.SetColor(System.Drawing.Color.White);
            }
            ws.Cells.AutoFitColumns();

            return p.GetAsByteArray();
        }

        // ================
        //    TRANSFERTS
        // ================

        /// <summary>
        /// Transfère une quantité d’une bouteille vers un autre site.
        /// - On décrémente la source.
        /// - On cherche (même nom+millésime+fournisseur) dans le site cible, sinon on clone.
        /// </summary>
        public bool Transfer(Guid bottleId, string fromSite, string toSite, int quantity)
            => Transfer(bottleId, fromSite, toSite, quantity, moveTags: false);

        /// <summary>Surcharge avec moveTags ignoré (conservée pour compat code appelant).</summary>
        public bool Transfer(Guid bottleId, string fromSite, string toSite, int quantity, bool moveTags)
        {
            if (quantity <= 0) return false;

            var src = _db.Bottles.FirstOrDefault(b => b.Id == bottleId && b.Location == fromSite);
            if (src == null) return false;
            if (src.Quantity < quantity) return false;

            src.Quantity -= quantity;

            var dst = _db.Bottles.FirstOrDefault(b =>
                b.Location == toSite &&
                b.Name == src.Name &&
                b.Vintage == src.Vintage &&
                b.Supplier == src.Supplier);

            if (dst == null)
            {
                dst = new Bottle
                {
                    Id = Guid.NewGuid(),
                    Color = src.Color,
                    Name = src.Name,
                    Appellation = src.Appellation,
                    Producer = src.Producer,
                    Country = src.Country,
                    Region = src.Region,
                    Supplier = src.Supplier,
                    Grapes = src.Grapes,
                    Vintage = src.Vintage,
                    PurchasePrice = src.PurchasePrice,
                    SalePrice = src.SalePrice,
                    Quantity = 0,
                    LowStockThreshold = src.LowStockThreshold,
                    Location = toSite
                };
                _db.Bottles.Add(dst);
            }

            dst.Quantity += quantity;

            _db.SaveChanges();
            return true;
        }

        // =================
        //   HELPERS PARSE
        // =================

        private static int ParseIntSafe(string? s, int def = 0)
        {
            if (int.TryParse((s ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                return i;

            var cleaned = (s ?? "").Replace(" ", "");
            return int.TryParse(cleaned, NumberStyles.Integer, CultureInfo.InvariantCulture, out i) ? i : def;
        }

        private static decimal ParseMoney(string? s, decimal def = 0m)
        {
            if (string.IsNullOrWhiteSpace(s)) return def;

            var txt = s.Trim()
                .Replace("€", "", StringComparison.OrdinalIgnoreCase)
                .Replace("EUR", "", StringComparison.OrdinalIgnoreCase)
                .Replace(" ", "");

            // French style "12,90"
            txt = txt.Replace(",", ".");

            return decimal.TryParse(txt, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out var d) ? d : def;
        }
    }
}
