using System;
using System.Collections.Generic;
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

        public OrderService(AppDbContext db, SupplierService suppliers)
        {
            _db = db;
            _suppliers = suppliers;
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
        public (string subject, string body, List<string> to) PrepareEmail(Order order)
        {
            // Affectations "safe" (supprime CS8601)
            var s = _suppliers.GetByName(order.SupplierName);
            order.SupplierName = (s?.Name ?? order.SupplierName ?? string.Empty).Trim();
            order.ContactName = (s?.ContactName ?? order.ContactName ?? string.Empty).Trim();
            order.OrderEmail = (s?.OrderEmail ?? order.OrderEmail ?? string.Empty).Trim();
            order.Phone = (s?.Phone ?? order.Phone ?? string.Empty).Trim();

            var subject = $"Commande {order.SupplierName} – {DateTime.Now:yyyy-MM-dd}";

            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(order.ContactName))
                sb.AppendLine($"Bonjour {order.ContactName},");
            else
                sb.AppendLine("Bonjour,");

            sb.AppendLine();
            sb.AppendLine("Veuillez trouver ci-dessous notre commande :");
            sb.AppendLine();

            foreach (var it in order.Lines.OrderBy(l => l.Site).ThenBy(l => l.Producer).ThenBy(l => l.Name))
            {
                sb.AppendLine($"- [{it.Site}] {it.Producer} – {it.Name} {(it.Vintage > 0 ? $"({it.Vintage})" : "")} x {it.Quantity}");
            }

            sb.AppendLine();
            sb.AppendLine("Merci d’en accuser réception.");
            sb.AppendLine("Cordialement,");

            var body = sb.ToString();

            // Destinataires (filtre + dé-nullification)
            var emails = new List<string?>();
            if (!string.IsNullOrWhiteSpace(order.OrderEmail)) emails.Add(order.OrderEmail);
            if (!string.IsNullOrWhiteSpace(s?.OrderEmail)) emails.Add(s!.OrderEmail);

            var to = (emails ?? new List<string?>())
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => e!) // sûr après Where
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
    }
}
