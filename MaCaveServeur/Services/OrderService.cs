using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression; // pour ZipArchive + System.IO.Compression.CompressionLevel
using System.Linq;
using System.Text;
using MaCaveServeur.Data;
using MaCaveServeur.Models;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;

namespace MaCaveServeur.Services
{
    /// <summary>
    /// Service de préparation des commandes fournisseurs à partir des bouteilles sous seuil.
    /// </summary>
    public class OrderService
    {
        private readonly AppDbContext _db;
        private readonly SupplierService _suppliers;
        private readonly AppState _state;
        private readonly HashSet<string> _blockedSuppliers;

        public OrderService(AppDbContext db, SupplierService suppliers, AppState state)
        {
            _db = db;
            _suppliers = suppliers;
            _state = state;
            _blockedSuppliers = BuildBlockedSuppliers(state);
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        // ---------------------------
        // Types exposés par le service
        // ---------------------------

        /// <summary>
        /// Une ligne "sous seuil" détectée dans la cave.
        /// </summary>
        public class ShortageLine
        {
            public Guid BottleId { get; set; }
            public string Supplier { get; set; } = "";
            public string Site { get; set; } = "";
            public string Producer { get; set; } = "";
            public string Name { get; set; } = "";
            public int Vintage { get; set; }
            public string Appellation { get; set; } = "";
            public string Color { get; set; } = "";
            public int CurrentQty { get; set; }
            public int LowStockThreshold { get; set; }
            public decimal PurchasePrice { get; set; }
            public string Region { get; set; } = "";
            public string Country { get; set; } = "";
        }

        /// <summary>
        /// Commande "mémoire" par fournisseur pour regrouper les lignes à commander.
        /// </summary>
        public class Order
        {
            public Guid Id { get; set; } = Guid.NewGuid();
            public string SupplierName { get; set; } = "";
            public string ContactName { get; set; } = "";
            public string OrderEmail { get; set; } = "";
            public string Phone { get; set; } = "";
            public List<OrderLine> Lines { get; set; } = new();
        }

        public class OrderLine
        {
            public string Site { get; set; } = "";
            public Guid BottleId { get; set; }
            public string Producer { get; set; } = "";
            public string Name { get; set; } = "";
            public int Vintage { get; set; }
            public int Quantity { get; set; } // quantité à commander
            public decimal PurchasePrice { get; set; }
            public string Appellation { get; set; } = "";
            public string Color { get; set; } = "";
        }

        // ---------------------------
        // Récupération des "sous seuil"
        // ---------------------------

        /// <summary>
        /// Retourne toutes les lignes sous le seuil (Quantity &lt; LowStockThreshold) pour toutes les caves.
        /// </summary>
        public List<ShortageLine> GetShortages()
        {
            // On charge minimalement les colonnes utiles
            var query = _db.Bottles
                .AsNoTracking()
                .Where(b => b.LowStockThreshold > 0 && b.Quantity < b.LowStockThreshold);

            var result = query
                .Select(b => new ShortageLine
                {
                    BottleId = b.Id,
                    Supplier = b.Supplier ?? "",
                    Site = b.SiteCode ?? "",
                    Producer = b.Producer ?? "",
                    Name = b.Name ?? "",
                    Vintage = b.Vintage,
                    Appellation = b.Appellation ?? "",
                    Color = b.Color ?? "",
                    CurrentQty = b.Quantity,
                    LowStockThreshold = b.LowStockThreshold,
                    PurchasePrice = b.PurchasePrice,
                    Region = b.Region ?? "",
                    Country = b.Country ?? ""
                })
                .ToList();

            foreach (var line in result)
            {
                line.Supplier = NormalizeSupplier(line.Supplier);
            }

            return result;
        }

        // ---------------------------
        // Construction d'une commande
        // ---------------------------

        /// <summary>
        /// Construit une commande pour un fournisseur à partir de lignes "sous seuil".
        /// </summary>
        public Order BuildOrderFromShortages(List<ShortageLine> shortagesOfSingleSupplier)
        {
            string supplier = shortagesOfSingleSupplier.FirstOrDefault()?.Supplier ?? "";

            var order = new Order
            {
                SupplierName = supplier
            };

            foreach (var s in shortagesOfSingleSupplier.OrderBy(x => x.Site).ThenBy(x => x.Producer).ThenBy(x => x.Name))
            {
                // Quantité à commander = manquant pour atteindre le seuil
                int toOrder = Math.Max(0, s.LowStockThreshold - s.CurrentQty);
                if (toOrder == 0) continue;

                order.Lines.Add(new OrderLine
                {
                    Site = s.Site,
                    BottleId = s.BottleId,
                    Producer = s.Producer,
                    Name = s.Name,
                    Vintage = s.Vintage,
                    Quantity = toOrder,
                    PurchasePrice = s.PurchasePrice,
                    Appellation = s.Appellation,
                    Color = s.Color
                });
            }

            // Renseigne les infos du fournisseur si connu
            var sp = _suppliers.GetByName(supplier);
            order.SupplierName = (sp?.Name ?? supplier ?? string.Empty).Trim();
            order.ContactName = (sp?.ContactName ?? string.Empty).Trim();
            order.OrderEmail = (sp?.OrderEmail ?? string.Empty).Trim();
            order.Phone = (sp?.Phone ?? string.Empty).Trim();

            return order;
        }

        // ---------------------------
        // Préparation du mail
        // ---------------------------

        /// <summary>
        /// Objet, corps et destinataires pour la commande par mail.
        /// </summary>
        public (string subject, string body, List<string> to) PrepareEmail(Order order, EmailOptions? options = null)
        {
            var s = _suppliers.GetByName(order.SupplierName);
            order.SupplierName = (s?.Name ?? order.SupplierName ?? string.Empty).Trim();
            order.ContactName = (s?.ContactName ?? order.ContactName ?? string.Empty).Trim();
            order.OrderEmail = (s?.OrderEmail ?? order.OrderEmail ?? string.Empty).Trim();
            order.Phone = (s?.Phone ?? order.Phone ?? string.Empty).Trim();
            var deliveryDate = options?.DeliveryDate ?? GetNextDeliveryDate();
            var fr = new CultureInfo("fr-FR");
            var dateLabel = deliveryDate.ToString("dd MMMM", fr);
            var subject = $"Commande - {order.SupplierName} - Livraison MERCREDI {dateLabel}";
            var body = BuildEmailBody(order, deliveryDate, options);
            var emails = new List<string?>();
            if (!string.IsNullOrWhiteSpace(order.OrderEmail)) emails.Add(order.OrderEmail);
            if (!string.IsNullOrWhiteSpace(s?.OrderEmail)) emails.Add(s!.OrderEmail);
            var to = (emails ?? new List<string?>())
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => e!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return (subject, body, to);
        }

        // ---------------------------
        // Exports
        // ---------------------------

        /// <summary>
        /// Fichier Excel (.xlsx) d’une commande pour un fournisseur.
        /// </summary>
        public byte[] BuildSupplierWorkbook(string supplierName)
        {
            var shortages = GetShortages()
                .Where(s => string.Equals(s.Supplier, supplierName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var order = BuildOrderFromShortages(shortages);

            using var p = new ExcelPackage();
            var ws = p.Workbook.Worksheets.Add("Commande");

            string[] headers =
            {
                "Site", "Producteur", "Nom", "Millésime", "Appellation",
                "Couleur", "Qté à commander", "Prix Achat", "Total Ligne"
            };

            for (int i = 0; i < headers.Length; i++)
                ws.Cells[1, i + 1].Value = headers[i];

            int r = 2;
            foreach (var l in order.Lines)
            {
                ws.Cells[r, 1].Value = l.Site;
                ws.Cells[r, 2].Value = l.Producer;
                ws.Cells[r, 3].Value = l.Name;
                ws.Cells[r, 4].Value = l.Vintage;
                ws.Cells[r, 5].Value = l.Appellation;
                ws.Cells[r, 6].Value = l.Color;
                ws.Cells[r, 7].Value = l.Quantity;
                ws.Cells[r, 8].Value = (double)l.PurchasePrice;
                ws.Cells[r, 9].Value = (double)(l.PurchasePrice * l.Quantity);
                r++;
            }

            // Total général
            ws.Cells[r, 8].Value = "TOTAL";
            ws.Cells[r, 9].Formula = $"SUM(I2:I{r - 1})";

            ws.Cells[1, 1, r, headers.Length].AutoFitColumns();

            return p.GetAsByteArray();
        }

        /// <summary>
        /// ZIP contenant un Excel par fournisseur (uniquement ceux qui ont des sous-seuils).
        /// </summary>
        public byte[] BuildAllSuppliersZip()
        {
            var shortages = GetShortages();
            var bySupplier = shortages
                .GroupBy(s => s.Supplier ?? "", StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key);

            using var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var g in bySupplier)
                {
                    var order = BuildOrderFromShortages(g.ToList());
                    if (order.Lines.Count == 0) continue;

                    var bytes = BuildSupplierWorkbook(order.SupplierName);
                    var safeName = SanitizeFileName(order.SupplierName);
                    var entry = zip.CreateEntry($"{safeName}_{DateTime.Now:yyyyMMdd}.xlsx",
                        System.IO.Compression.CompressionLevel.Optimal); // <— fully qualified

                    using var es = entry.Open();
                    es.Write(bytes, 0, bytes.Length);
                }
            }
            return ms.ToArray();
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Fournisseur";
            var invalid = Path.GetInvalidFileNameChars();
            var safe = new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
            return safe.Trim();
        }

        private static DateTime GetNextDeliveryDate()
        {
            var today = DateTime.Today;
            var daysUntil = ((int)DayOfWeek.Wednesday - (int)today.DayOfWeek + 7) % 7;
            if (daysUntil == 0) daysUntil = 7;
            return today.AddDays(daysUntil);
        }

        public class EmailOptions
        {
            public DateTime? DeliveryDate { get; set; }
            public string? IntroNote { get; set; }
            public string? DeliveryAddress { get; set; }
            public string? FooterNote { get; set; }
            public string? SignatureName { get; set; }
            public string? SignatureRole { get; set; }
            public string? SignatureSites { get; set; }
        }

        private static string BuildEmailBody(Order order, DateTime deliveryDate, EmailOptions? options)
        {
            var fr = new CultureInfo("fr-FR");
            var dateLabel = deliveryDate.ToString("dd MMMM", fr);
            var introNote = string.IsNullOrWhiteSpace(options?.IntroNote)
                ? "Bonjour,"
                : options!.IntroNote!.Trim();
            var deliveryAddress = string.IsNullOrWhiteSpace(options?.DeliveryAddress)
                ? "Brasserie Maillard, 17 rue Saint Rémy, 33000 Bordeaux"
                : options!.DeliveryAddress!.Trim();
            var footerNote = string.IsNullOrWhiteSpace(options?.FooterNote)
                ? "Merci de bien facturer les bons établissements"
                : options!.FooterNote!.Trim();
            var signatureName = string.IsNullOrWhiteSpace(options?.SignatureName) ? "ETHAN FONTAINE" : options!.SignatureName!.Trim();
            var signatureRole = string.IsNullOrWhiteSpace(options?.SignatureRole) ? "SOMMELIER // www.maison-amour.fr" : options!.SignatureRole!.Trim();
            var signatureSites = string.IsNullOrWhiteSpace(options?.SignatureSites) ? "Brutus, Brasserie Maillard, Gramma, Bacchus, Merlot, Banquet" : options!.SignatureSites!.Trim();
            var sb = new StringBuilder();
            sb.AppendLine(introNote);
            sb.AppendLine();
            var linesBySite = order.Lines
                .OrderBy(l => l.Site)
                .ThenBy(l => l.Producer)
                .ThenBy(l => l.Name)
                .GroupBy(l => l.Site);
            foreach (var siteGroup in linesBySite)
            {
                if (string.IsNullOrWhiteSpace(siteGroup.Key)) continue;
                sb.AppendLine($"POUR {siteGroup.Key} :");
                sb.AppendLine();
                foreach (var it in siteGroup)
                {
                    var parts = new List<string>();
                    if (!string.IsNullOrWhiteSpace(it.Appellation)) parts.Add(it.Appellation);
                    if (!string.IsNullOrWhiteSpace(it.Producer)) parts.Add(it.Producer);
                    if (!string.IsNullOrWhiteSpace(it.Name)) parts.Add(it.Name);
                    var line = string.Join(", ", parts);
                    if (it.Vintage > 0) line += $", {it.Vintage}";
                    line += $" X {it.Quantity}";
                    sb.AppendLine(line);
                }
                sb.AppendLine();
            }
            sb.AppendLine(footerNote);
            sb.AppendLine();
            sb.AppendLine($"Livraison MERCREDI {dateLabel} a {deliveryAddress}");
            sb.AppendLine();
            sb.AppendLine("Bonne journée");
            sb.AppendLine();
            sb.AppendLine(signatureName);
            sb.AppendLine(signatureRole);
            sb.AppendLine(signatureSites);
            return sb.ToString();
        }

        private string NormalizeSupplier(string? supplier)
        {
            var clean = NormalizeDisplay(supplier);
            if (string.IsNullOrWhiteSpace(clean)) return "Sans fournisseur";

            var key = NormalizeKey(clean);
            if (_blockedSuppliers.Contains(key)) return "Sans fournisseur";

            var known = _suppliers.GetByName(clean);
            if (known != null && !string.IsNullOrWhiteSpace(known.Name))
                return known.Name.Trim();

            if (IsAllUpper(clean))
                return ToTitleCase(clean);

            return clean;
        }

        private static HashSet<string> BuildBlockedSuppliers(AppState state)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var site in state.AllSites)
            {
                if (site.Code.Equals("ALL", StringComparison.OrdinalIgnoreCase)) continue;
                set.Add(NormalizeKey(site.Code));
                set.Add(NormalizeKey(site.Label));
            }
            return set;
        }

        private static string NormalizeKey(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var s = value.Trim();
            var sb = new StringBuilder(s.Length);
            var prevSpace = false;
            foreach (var ch in s)
            {
                if (char.IsWhiteSpace(ch))
                {
                    if (!prevSpace)
                    {
                        sb.Append(' ');
                        prevSpace = true;
                    }
                }
                else
                {
                    sb.Append(char.ToLowerInvariant(ch));
                    prevSpace = false;
                }
            }
            return sb.ToString();
        }

        private static string NormalizeDisplay(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var s = value.Trim();
            var sb = new StringBuilder(s.Length);
            var prevSpace = false;
            foreach (var ch in s)
            {
                if (char.IsWhiteSpace(ch))
                {
                    if (!prevSpace)
                    {
                        sb.Append(' ');
                        prevSpace = true;
                    }
                }
                else
                {
                    sb.Append(ch);
                    prevSpace = false;
                }
            }
            return sb.ToString();
        }

        private static bool IsAllUpper(string value)
        {
            var hasLetter = false;
            foreach (var ch in value)
            {
                if (!char.IsLetter(ch)) continue;
                hasLetter = true;
                if (!char.IsUpper(ch)) return false;
            }
            return hasLetter;
        }

        private static string ToTitleCase(string value)
        {
            var lower = value.ToLowerInvariant();
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(lower);
        }
    }
}


