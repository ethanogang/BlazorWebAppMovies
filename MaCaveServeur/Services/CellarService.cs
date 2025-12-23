using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Globalization;
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

        // ------------------ API "nouvelle" ------------------
        public List<Bottle> GetAll() => _db.Bottles.AsNoTracking().ToList();

        public Bottle? Get(Guid id) => _db.Bottles.Find(id);

        /// <summary>
        /// Ajout “intelligent” : si doublon détecté -> merge quantité, sinon création.
        /// </summary>
        public void Add(Bottle b)
        {
            BottleNormalization.NormalizeInPlace(b);
            if (b.Id == Guid.Empty) b.Id = Guid.NewGuid();

            // Doublon ? -> merge
            var existing = FindByBusinessKey(b);
            if (existing != null)
            {
                MergeInto(existing, b, addQuantity: true);
                _db.SaveChanges();
                return;
            }

            _db.Bottles.Add(b);
            _db.SaveChanges();
        }

        /// <summary>
        /// Upsert par ID (édition) : met à jour l’enregistrement existant.
        /// </summary>
        public void Upsert(Bottle b)
        {
            BottleNormalization.NormalizeInPlace(b);
            if (b.Id == Guid.Empty) b.Id = Guid.NewGuid();

            var existing = _db.Bottles.FirstOrDefault(x => x.Id == b.Id);
            if (existing == null)
            {
                // Si l'ID n'existe pas, on applique aussi la logique anti-doublon
                Add(b);
                return;
            }

            MergeInto(existing, b, addQuantity: false);
            _db.SaveChanges();
        }

        public void Delete(Guid id)
        {
            var b = _db.Bottles.Find(id);
            if (b == null) return;

            _db.Bottles.Remove(b);
            _db.SaveChanges();
        }

        // ------------------ API "ancienne" (wrappers Wine) ------------------
        public List<Bottle> GetAllWines() => GetAll();
        public Bottle? GetWine(Guid id) => Get(id);
        public void AddWine(Bottle b) => Add(b);
        public void UpdateWine(Bottle b) => Upsert(b);
        public void DeleteWine(Guid id) => Delete(id);

        // ------------------ Transfer ------------------
        public bool Transfer(Guid bottleId, string fromSite, string toSite, int quantity)
        {
            if (quantity <= 0) return false;

            var srcSite = Sites.NormalizeOrThrow(fromSite);
            var dstSite = Sites.NormalizeOrThrow(toSite);
            if (srcSite == dstSite) return false;

            var src = _db.Bottles.FirstOrDefault(b => b.Id == bottleId && b.SiteCode == srcSite);
            if (src == null) return false;
            if (src.Quantity < quantity) return false;

            // Trouve une bouteille “équivalente” sur le site cible
            var probe = new Bottle
            {
                SiteCode = dstSite,
                Producer = src.Producer,
                Name = src.Name,
                Vintage = src.Vintage,
                Supplier = src.Supplier
            };
            BottleNormalization.NormalizeInPlace(probe);

            var dst = FindByBusinessKey(probe);

            if (dst == null)
            {
                dst = new Bottle
                {
                    Id = Guid.NewGuid(),
                    SiteCode = dstSite,
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
                    LowStockThreshold = src.LowStockThreshold,
                    Quantity = 0,
                    Notes = src.Notes
                };
                BottleNormalization.NormalizeInPlace(dst);
                _db.Bottles.Add(dst);
            }

            src.Quantity -= quantity;
            dst.Quantity += quantity;

            _db.SaveChanges();
            return true;
        }

        // ------------------ Import/Export Excel ------------------
        /// <summary>
        /// Import “anti-doublons” : si la clé métier existe, on MERGE (additionne la quantité).
        /// </summary>
        public int ImportExcel(Stream excelStream, string siteCode)
        {
            var normalizedSite = Sites.NormalizeOrThrow(siteCode);

            using var p = new ExcelPackage(excelStream);
            var ws = p.Workbook.Worksheets[0];

            var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int col = 1;
            while (!string.IsNullOrWhiteSpace(ws.Cells[1, col].Text))
            {
                headers[ws.Cells[1, col].Text.Trim()] = col;
                col++;
            }

            int GetCol(string header) => headers.TryGetValue(header, out var c) ? c : -1;

            var cColor = GetCol("COULEUR");
            var cName = GetCol("NOM");
            var cApp = GetCol("APPELATION");
            var cProd = GetCol("PRODUCTEUR");
            var cCountry = GetCol("PAYS");
            var cRegion = GetCol("REGION");
            var cSupplier = GetCol("FOURNISSEUR");
            var cVintage = GetCol("MILLESIME");
            var cGrapes = GetCol("CEPAGES");
            var cBuy = GetCol("PRIX ACHAT");
            var cSell = GetCol("PRIX VENTE");
            var cLow = GetCol("SEUIL ALERTE");
            var cQty = GetCol("QUANTITE");

            int created = 0;
            int merged = 0;

            int row = 2;
            while (!string.IsNullOrWhiteSpace(ws.Cells[row, cName > 0 ? cName : 1].Text))
            {
                var incoming = new Bottle
                {
                    Id = Guid.NewGuid(),
                    SiteCode = normalizedSite,
                    Color = cColor > 0 ? ws.Cells[row, cColor].Text : null,
                    Name = cName > 0 ? ws.Cells[row, cName].Text : null,
                    Appellation = cApp > 0 ? ws.Cells[row, cApp].Text : null,
                    Producer = cProd > 0 ? ws.Cells[row, cProd].Text : null,
                    Country = cCountry > 0 ? ws.Cells[row, cCountry].Text : null,
                    Region = cRegion > 0 ? ws.Cells[row, cRegion].Text : null,
                    Supplier = cSupplier > 0 ? ws.Cells[row, cSupplier].Text : null,
                    Vintage = cVintage > 0 ? ParseIntSafe(ws.Cells[row, cVintage].Text) : 0,
                    Grapes = cGrapes > 0 ? ws.Cells[row, cGrapes].Text : null,
                    PurchasePrice = cBuy > 0 ? ParseMoney(ws.Cells[row, cBuy].Text) : 0m,
                    SalePrice = cSell > 0 ? ParseMoney(ws.Cells[row, cSell].Text) : 0m,
                    LowStockThreshold = cLow > 0 ? ParseIntSafe(ws.Cells[row, cLow].Text) : 0,
                    Quantity = cQty > 0 ? ParseIntSafe(ws.Cells[row, cQty].Text) : 0
                };

                BottleNormalization.NormalizeInPlace(incoming);

                var existing = FindByBusinessKey(incoming);
                if (existing != null)
                {
                    // Merge : on additionne quantité, et on met à jour ce qui manque
                    MergeInto(existing, incoming, addQuantity: true);
                    merged++;
                }
                else
                {
                    _db.Bottles.Add(incoming);
                    created++;
                }

                row++;
            }

            _db.SaveChanges();
            return created + merged;
        }

        public byte[] ExportExcel(string? siteCode = null)
        {
            var normalizedSite = Sites.NormalizeOrNull(siteCode);

            var list = _db.Bottles.AsNoTracking()
                .Where(b => normalizedSite == null || b.SiteCode == normalizedSite)
                .OrderBy(b => b.SiteCode).ThenBy(b => b.Producer).ThenBy(b => b.Name)
                .ToList();

            using var p = new ExcelPackage();
            var ws = p.Workbook.Worksheets.Add("Cave");

            var headers = new[]
            {
                "COULEUR","NOM","APPELATION","PRODUCTEUR","PAYS","REGION","FOURNISSEUR","MILLESIME","CEPAGES","PRIX ACHAT","PRIX VENTE","SEUIL ALERTE","QUANTITE","SITE"
            };

            for (int c = 0; c < headers.Length; c++)
            {
                ws.Cells[1, c + 1].Value = headers[c];
                ws.Cells[1, c + 1].Style.Font.Bold = true;
                ws.Cells[1, c + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[1, c + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
            }

            int r = 2;
            foreach (var b in list)
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
                ws.Cells[r, 10].Value = b.PurchasePrice;
                ws.Cells[r, 11].Value = b.SalePrice;
                ws.Cells[r, 12].Value = b.LowStockThreshold;
                ws.Cells[r, 13].Value = b.Quantity;
                ws.Cells[r, 14].Value = b.SiteCode;
                r++;
            }

            ws.Cells[ws.Dimension.Address].AutoFitColumns();
            return p.GetAsByteArray();
        }

        // ------------------ Helpers anti-doublons ------------------

        private Bottle? FindByBusinessKey(Bottle b)
        {
            // b doit déjà être normalisée (NormalizeInPlace)
            var site = b.SiteCode;
            var producer = b.Producer;
            var name = b.Name;
            var vintage = b.Vintage;
            var supplier = b.Supplier;

            // On cherche strictement (après normalisation)
            return _db.Bottles.FirstOrDefault(x =>
                x.SiteCode == site &&
                x.Producer == producer &&
                x.Name == name &&
                x.Vintage == vintage &&
                x.Supplier == supplier);
        }

        private static void MergeInto(Bottle target, Bottle incoming, bool addQuantity)
        {
            // On suppose incoming normalisée.
            // Champs “identité” (clé) déjà identiques.

            // Quantité
            if (addQuantity)
                target.Quantity += incoming.Quantity;
            else
                target.Quantity = incoming.Quantity;

            // Mise à jour des infos si incoming apporte quelque chose
            target.Color = incoming.Color ?? target.Color;
            target.Appellation = incoming.Appellation ?? target.Appellation;
            target.Country = incoming.Country ?? target.Country;
            target.Region = incoming.Region ?? target.Region;
            target.Grapes = incoming.Grapes ?? target.Grapes;

            // Prix : si incoming non nul (et >0) alors on garde
            if (incoming.PurchasePrice > 0) target.PurchasePrice = incoming.PurchasePrice;
            if (incoming.SalePrice > 0) target.SalePrice = incoming.SalePrice;

            // Seuil : si incoming >0, on garde
            if (incoming.LowStockThreshold > 0) target.LowStockThreshold = incoming.LowStockThreshold;

            // Notes : si incoming non vide, on remplace
            if (!string.IsNullOrWhiteSpace(incoming.Notes)) target.Notes = incoming.Notes;

            // RFID / last seen (optionnel)
            target.RfidTagsSerialized = incoming.RfidTagsSerialized ?? target.RfidTagsSerialized;
            target.LastSeenUtc = incoming.LastSeenUtc ?? target.LastSeenUtc;
        }

        private static int ParseIntSafe(string? s, int def = 0)
            => int.TryParse((s ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : def;

        private static decimal ParseMoney(string? s, decimal def = 0m)
        {
            if (string.IsNullOrWhiteSpace(s)) return def;

            var txt = s.Trim()
                .Replace("€", "", StringComparison.OrdinalIgnoreCase)
                .Replace("EUR", "", StringComparison.OrdinalIgnoreCase)
                .Replace(" ", "");

            txt = txt.Replace(",", ".");

            return decimal.TryParse(txt,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out var d) ? d : def;
        }
    }
}
