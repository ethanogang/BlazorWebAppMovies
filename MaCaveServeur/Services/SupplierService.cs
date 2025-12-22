using System.Globalization;
using MaCaveServeur.Data;
using MaCaveServeur.Models;
using Microsoft.EntityFrameworkCore;

// EPPlus (v7)
using OfficeOpenXml;
using OfficeOpenXml.Table;

namespace MaCaveServeur.Services;

public class SupplierService
{
    private readonly AppDbContext _db;

    public SupplierService(AppDbContext db)
    {
        _db = db;
    }

    // ----------------------------------------------------------------
    // Lecture
    // ----------------------------------------------------------------
    public async Task<List<Supplier>> AllAsync() =>
        await _db.Suppliers
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .ToListAsync();

    public async Task<Supplier?> ByIdAsync(Guid id) =>
        await _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);

    /// <summary>Recherche exact (insensible à la casse) par nom.</summary>
    public Supplier? GetByName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var n = name.Trim().ToLowerInvariant();
        return _db.Suppliers.AsNoTracking().FirstOrDefault(s => s.Name.ToLower() == n);
    }

    public async Task<Supplier?> GetByNameAsync(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var n = name.Trim().ToLowerInvariant();
        return await _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Name.ToLower() == n);
    }

    // ----------------------------------------------------------------
    // Écriture
    // ----------------------------------------------------------------
    public async Task<Supplier> UpsertAsync(Supplier s)
    {
        // Normalise le nom pour éviter des doublons dus aux espaces/casse
        s.Name = (s.Name ?? string.Empty).Trim();

        if (s.Id == Guid.Empty)
        {
            // Déduplication sur le nom
            var existing = await _db.Suppliers.FirstOrDefaultAsync(x => x.Name.ToLower() == s.Name.ToLower());
            if (existing is not null)
            {
                // Mise à jour de l'existant
                CopyEditable(existing, s);
                await _db.SaveChangesAsync();
                return existing;
            }

            s.Id = Guid.NewGuid();
            _db.Suppliers.Add(s);
        }
        else
        {
            var existing = await _db.Suppliers.FirstOrDefaultAsync(x => x.Id == s.Id);
            if (existing is null)
            {
                _db.Suppliers.Add(s);
            }
            else
            {
                CopyEditable(existing, s);
            }
        }

        await _db.SaveChangesAsync();
        return s;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var s = await _db.Suppliers.FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return false;
        _db.Suppliers.Remove(s);
        await _db.SaveChangesAsync();
        return true;
    }

    private static void CopyEditable(Supplier target, Supplier src)
    {
        target.Name        = (src.Name ?? string.Empty).Trim();
        target.ContactName = src.ContactName?.Trim();
        target.OrderEmail  = src.OrderEmail?.Trim();
        target.Phone       = src.Phone?.Trim();
        target.Notes       = src.Notes?.Trim();
    }

    // ----------------------------------------------------------------
    // Import / Export Excel (EPPlus 7)
    // ----------------------------------------------------------------

    /// <summary>
    /// Importe une liste de fournisseurs depuis un fichier Excel (flux).
    /// L'onglet lu est le premier. Les entêtes reconnus (insensible à la casse) :
    /// - Nom / Fournisseur / Supplier
    /// - Contact / Commercial / Sales rep
    /// - Email / Mail / E-mail / Courriel
    /// - Phone / Téléphone / Tel
    /// - Notes / Remarques
    /// </summary>
    public async Task<(int inserted, int updated)> ImportExcelAsync(Stream excelStream)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        using var pck = new ExcelPackage();
        await pck.LoadAsync(excelStream);

        var ws = pck.Workbook.Worksheets.FirstOrDefault();
        if (ws is null) return (0, 0);

        // Map colonnes
        var map = BuildHeaderMap(ws);
        if (!map.TryGetValue("name", out var colName) || colName <= 0)
            throw new InvalidOperationException("Colonne 'Nom' / 'Fournisseur' / 'Supplier' introuvable.");

        var row = 2;
        var inserted = 0;
        var updated  = 0;

        while (true)
        {
            var rawName = ws.Cells[row, colName].Text?.Trim();
            if (string.IsNullOrWhiteSpace(rawName))
                break; // fin

            var s = new Supplier
            {
                Name        = rawName,
                ContactName = Read(ws, row, map, "contact"),
                OrderEmail  = Read(ws, row, map, "email"),
                Phone       = Read(ws, row, map, "phone"),
                Notes       = Read(ws, row, map, "notes"),
            };

            var existed = await _db.Suppliers.FirstOrDefaultAsync(x => x.Name.ToLower() == s.Name.ToLower());
            if (existed is null)
            {
                s.Id = Guid.NewGuid();
                _db.Suppliers.Add(s);
                inserted++;
            }
            else
            {
                CopyEditable(existed, s);
                updated++;
            }

            row++;
        }

        await _db.SaveChangesAsync();
        return (inserted, updated);
    }

    /// <summary>
    /// Export Excel des fournisseurs (colonnes: Supplier, Contact, Email, Phone, Notes).
    /// </summary>
    public async Task<byte[]> ExportExcelAsync()
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var pck = new ExcelPackage();
        var ws = pck.Workbook.Worksheets.Add("Fournisseurs");

        // En-têtes
        ws.Cells[1, 1].Value = "Supplier";
        ws.Cells[1, 2].Value = "Contact";
        ws.Cells[1, 3].Value = "Email";
        ws.Cells[1, 4].Value = "Phone";
        ws.Cells[1, 5].Value = "Notes";

        var list = await _db.Suppliers
                            .AsNoTracking()
                            .OrderBy(s => s.Name)
                            .ToListAsync();

        var r = 2;
        foreach (var s in list)
        {
            ws.Cells[r, 1].Value = s.Name;
            ws.Cells[r, 2].Value = s.ContactName;
            ws.Cells[r, 3].Value = s.OrderEmail;
            ws.Cells[r, 4].Value = s.Phone;
            ws.Cells[r, 5].Value = s.Notes;
            r++;
        }

        // Table + auto-fit
        var tableRange = ws.Cells[1, 1, Math.Max(1, r - 1), 5];
        var table = ws.Tables.Add(tableRange, "tblSuppliers");
        table.TableStyle = TableStyles.Medium2;
        ws.Cells.AutoFitColumns();

        return await pck.GetAsByteArrayAsync();
    }

    // ----------------------------------------------------------------
    // Helpers Excel
    // ----------------------------------------------------------------
    private static Dictionary<string, int> BuildHeaderMap(ExcelWorksheet ws)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        int col = 1;
        while (true)
        {
            var h = ws.Cells[1, col].Text?.Trim();
            if (string.IsNullOrWhiteSpace(h)) break;

            var key = NormalizeHeader(h);
            if (!string.IsNullOrEmpty(key) && !map.ContainsKey(key))
                map[key] = col;

            col++;
        }

        return map;
    }

    private static string NormalizeHeader(string header)
    {
        var h = header.Trim().ToLowerInvariant();

        return h switch
        {
            "nom" or "fournisseur" or "supplier"                         => "name",
            "contact" or "commercial" or "sales rep" or "salesrep"       => "contact",
            "email" or "e-mail" or "mail" or "courriel"                  => "email",
            "phone" or "téléphone" or "telephone" or "tel"               => "phone",
            "notes" or "remarques"                                       => "notes",
            _ => string.Empty
        };
    }

    private static string? Read(ExcelWorksheet ws, int row, Dictionary<string, int> map, string key)
    {
        return map.TryGetValue(key, out var col) && col > 0
            ? ws.Cells[row, col].Text?.Trim()
            : null;
    }
}
