using System.Globalization;
using System.Text;
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

    public async Task<(int merged, int removed)> DedupeAsync()
    {
        var all = await _db.Suppliers.OrderBy(s => s.Name).ToListAsync();
        var groups = all
            .GroupBy(s => NormalizeKey(s.Name))
            .Where(g => g.Count() > 1)
            .ToList();

        var merged = 0;
        var removed = 0;

        foreach (var g in groups)
        {
            var list = g.ToList();
            var target = list
                .OrderByDescending(ScoreSupplier)
                .First();

            foreach (var other in list.Where(x => x.Id != target.Id))
            {
                MergeSupplier(target, other);
                _db.Suppliers.Remove(other);
                merged++;
                removed++;
            }
        }

        await _db.SaveChangesAsync();
        return (merged, removed);
    }

    public async Task<(int removedSuppliers, int addedSuppliers, int updatedBottles)> SyncFromMasterAsync(
        Stream suppliersExcel,
        Stream masterExcel)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        var allowedSuppliers = ReadAllowedSuppliers(suppliersExcel);
        if (allowedSuppliers.Count == 0)
            throw new InvalidOperationException("Aucun fournisseur valide dans le fichier.");

        var existing = await _db.Suppliers.ToListAsync();
        var removedSuppliers = 0;
        var addedSuppliers = 0;

        foreach (var s in existing)
        {
            var key = NormalizeKey(s.Name);
            if (!allowedSuppliers.ContainsKey(key))
            {
                _db.Suppliers.Remove(s);
                removedSuppliers++;
            }
            else
            {
                var canonical = allowedSuppliers[key];
                if (!string.Equals(s.Name?.Trim(), canonical, StringComparison.Ordinal))
                    s.Name = canonical;
            }
        }

        foreach (var kv in allowedSuppliers)
        {
            if (!existing.Any(s => NormalizeKey(s.Name) == kv.Key))
            {
                _db.Suppliers.Add(new Supplier { Id = Guid.NewGuid(), Name = kv.Value });
                addedSuppliers++;
            }
        }

        await _db.SaveChangesAsync();

        var mappings = ReadWineSupplierMap(masterExcel, allowedSuppliers);
        var bottles = await _db.Bottles.ToListAsync();
        var updatedBottles = 0;

        foreach (var b in bottles)
        {
            var currentKey = NormalizeKey(b.Supplier);
            if (string.IsNullOrWhiteSpace(currentKey))
            {
                if (TryMapSupplier(b, mappings, allowedSuppliers, out var supplier))
                {
                    b.Supplier = supplier;
                    updatedBottles++;
                }
            }
            else if (allowedSuppliers.TryGetValue(currentKey, out var canonical))
            {
                if (!string.Equals(b.Supplier?.Trim(), canonical, StringComparison.Ordinal))
                {
                    b.Supplier = canonical;
                    updatedBottles++;
                }
            }
            else
            {
                if (TryMapSupplier(b, mappings, allowedSuppliers, out var supplier))
                {
                    b.Supplier = supplier;
                    updatedBottles++;
                }
                else
                {
                    b.Supplier = string.Empty;
                    updatedBottles++;
                }
            }
        }

        await _db.SaveChangesAsync();
        return (removedSuppliers, addedSuppliers, updatedBottles);
    }

    public async Task<int> DeleteBottlesWithInvalidSupplierAsync(Stream suppliersExcel)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        var allowedSuppliers = ReadAllowedSuppliers(suppliersExcel);
        if (allowedSuppliers.Count == 0)
            throw new InvalidOperationException("Aucun fournisseur valide dans le fichier.");

        var bottles = await _db.Bottles.ToListAsync();
        var toDelete = new List<Bottle>();

        foreach (var b in bottles)
        {
            var key = NormalizeKey(b.Supplier);
            if (string.IsNullOrWhiteSpace(key) || !allowedSuppliers.ContainsKey(key))
                toDelete.Add(b);
        }

        if (toDelete.Count == 0) return 0;

        _db.Bottles.RemoveRange(toDelete);
        await _db.SaveChangesAsync();
        return toDelete.Count;
    }

    private static void CopyEditable(Supplier target, Supplier src)
    {
        target.Name        = (src.Name ?? string.Empty).Trim();
        target.ContactName = src.ContactName?.Trim();
        target.OrderEmail  = src.OrderEmail?.Trim();
        target.Phone       = src.Phone?.Trim();
        target.Notes       = src.Notes?.Trim();
    }

    private static void MergeSupplier(Supplier target, Supplier src)
    {
        var bestName = ChooseBestName(target.Name, src.Name);
        if (!string.Equals(bestName, target.Name, StringComparison.Ordinal))
            target.Name = bestName;

        if (string.IsNullOrWhiteSpace(target.ContactName) && !string.IsNullOrWhiteSpace(src.ContactName))
            target.ContactName = src.ContactName?.Trim();

        if (string.IsNullOrWhiteSpace(target.OrderEmail) && !string.IsNullOrWhiteSpace(src.OrderEmail))
            target.OrderEmail = src.OrderEmail?.Trim();

        if (string.IsNullOrWhiteSpace(target.Phone) && !string.IsNullOrWhiteSpace(src.Phone))
            target.Phone = src.Phone?.Trim();

        if (string.IsNullOrWhiteSpace(target.Notes) && !string.IsNullOrWhiteSpace(src.Notes))
            target.Notes = src.Notes?.Trim();
    }

    private static int ScoreSupplier(Supplier s)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(s.ContactName)) score++;
        if (!string.IsNullOrWhiteSpace(s.OrderEmail)) score++;
        if (!string.IsNullOrWhiteSpace(s.Phone)) score++;
        if (!string.IsNullOrWhiteSpace(s.Notes)) score++;
        if (!string.IsNullOrWhiteSpace(s.Name)) score++;
        return score;
    }

    private static string ChooseBestName(string? a, string? b)
    {
        var sa = (a ?? string.Empty).Trim();
        var sb = (b ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(sa)) return sb;
        if (string.IsNullOrWhiteSpace(sb)) return sa;

        if (IsAllUpper(sa) && !IsAllUpper(sb)) return sb;
        if (IsAllUpper(sb) && !IsAllUpper(sa)) return sa;

        return sb.Length > sa.Length ? sb : sa;
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

    private static string NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var raw = RemoveDiacritics(value).ToLowerInvariant();
        var sb = new StringBuilder(raw.Length);
        var prevSpace = false;

        foreach (var ch in raw)
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
                prevSpace = false;
            }
            else
            {
                if (!prevSpace)
                {
                    sb.Append(' ');
                    prevSpace = true;
                }
            }
        }

        var tokens = sb.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => !IsNoiseToken(t))
            .ToList();

        return string.Join(' ', tokens);
    }

    private static bool IsNoiseToken(string token)
    {
        return token is
            "sas" or "sarl" or "sa" or "sasu" or "eurl" or "cie" or "co" or
            "company" or "compagnie" or "societe" or "soc" or
            "et" or "and" or "de" or "des" or "du" or "la" or "le" or "les";
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static Dictionary<string, string> ReadAllowedSuppliers(Stream suppliersExcel)
    {
        using var pck = new ExcelPackage();
        pck.Load(suppliersExcel);
        var ws = pck.Workbook.Worksheets.FirstOrDefault();
        if (ws == null) return new Dictionary<string, string>();

        var headerMap = BuildHeaderMapNormalized(ws);
        if (!headerMap.TryGetValue("FOURNISSEUR", out var colSupplier) || colSupplier <= 0)
            throw new InvalidOperationException("Colonne 'Fournisseur' introuvable dans le fichier fournisseurs.");

        var allowed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var row = 2;
        while (true)
        {
            var raw = ws.Cells[row, colSupplier].Text?.Trim();
            if (string.IsNullOrWhiteSpace(raw)) break;

            var key = NormalizeKey(raw);
            if (string.IsNullOrWhiteSpace(key))
            {
                row++;
                continue;
            }

            if (allowed.TryGetValue(key, out var existing))
                allowed[key] = ChooseBestName(existing, raw);
            else
                allowed[key] = raw.Trim();

            row++;
        }

        return allowed;
    }

    private static Dictionary<string, string> ReadWineSupplierMap(
        Stream masterExcel,
        Dictionary<string, string> allowedSuppliers)
    {
        using var pck = new ExcelPackage();
        pck.Load(masterExcel);
        var ws = pck.Workbook.Worksheets.FirstOrDefault();
        if (ws == null) return new Dictionary<string, string>();

        var headerMap = BuildHeaderMapNormalized(ws);
        if (!headerMap.TryGetValue("NOMVIN", out var colName) || colName <= 0)
            throw new InvalidOperationException("Colonne 'NOM VIN' introuvable dans le fichier maitre.");

        headerMap.TryGetValue("MILLESIMES", out var colVintage);
        headerMap.TryGetValue("FOURNISSEURS", out var colSupplier);

        if (colSupplier <= 0)
            throw new InvalidOperationException("Colonne 'FOURNISSEURS' introuvable dans le fichier maitre.");

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var row = 2;
        while (true)
        {
            var rawName = ws.Cells[row, colName].Text?.Trim();
            if (string.IsNullOrWhiteSpace(rawName)) break;

            var supplierRaw = ws.Cells[row, colSupplier].Text?.Trim();
            if (string.IsNullOrWhiteSpace(supplierRaw))
            {
                row++;
                continue;
            }

            var supplier = PickSupplier(supplierRaw, allowedSuppliers);
            if (string.IsNullOrWhiteSpace(supplier))
            {
                row++;
                continue;
            }

            var nameKey = NormalizeKey(rawName);
            if (string.IsNullOrWhiteSpace(nameKey))
            {
                row++;
                continue;
            }

            var vintage = 0;
            if (colVintage > 0)
                int.TryParse(ws.Cells[row, colVintage].Text?.Trim(), out vintage);

            if (vintage > 0)
            {
                var key = $"{nameKey}|{vintage}";
                if (!map.ContainsKey(key))
                    map[key] = supplier;
            }

            if (!map.ContainsKey(nameKey))
                map[nameKey] = supplier;

            row++;
        }

        return map;
    }

    private static string PickSupplier(string raw, Dictionary<string, string> allowedSuppliers)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var parts = raw.Split(new[] { ',', ';', '/', '|'}, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var p = part.Trim();
            if (string.IsNullOrWhiteSpace(p)) continue;
            var key = NormalizeKey(p);
            if (allowedSuppliers.TryGetValue(key, out var canonical))
                return canonical;
        }
        return raw.Trim();
    }

    private static bool TryMapSupplier(
        Bottle b,
        Dictionary<string, string> map,
        Dictionary<string, string> allowedSuppliers,
        out string supplier)
    {
        supplier = string.Empty;
        var name = !string.IsNullOrWhiteSpace(b.Name) ? b.Name : b.Designation;
        var nameKey = NormalizeKey(name);
        if (string.IsNullOrWhiteSpace(nameKey)) return false;

        if (b.Vintage > 0)
        {
            var key = $"{nameKey}|{b.Vintage}";
            if (map.TryGetValue(key, out var s))
            {
                var skey = NormalizeKey(s);
                if (allowedSuppliers.TryGetValue(skey, out var canonical))
                {
                    supplier = canonical;
                    return true;
                }
            }
        }

        if (map.TryGetValue(nameKey, out var s2))
        {
            var skey = NormalizeKey(s2);
            if (allowedSuppliers.TryGetValue(skey, out var canonical))
            {
                supplier = canonical;
                return true;
            }
        }

        return false;
    }

    private static Dictionary<string, int> BuildHeaderMapNormalized(ExcelWorksheet ws)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var col = 1;
        while (true)
        {
            var raw = ws.Cells[1, col].Text?.Trim();
            if (string.IsNullOrWhiteSpace(raw)) break;

            var key = NormalizeHeaderKey(raw);
            if (!string.IsNullOrWhiteSpace(key) && !map.ContainsKey(key))
                map[key] = col;

            col++;
        }
        return map;
    }

    private static string NormalizeHeaderKey(string header)
    {
        var clean = RemoveDiacritics(header).ToUpperInvariant();
        var sb = new StringBuilder(clean.Length);
        foreach (var ch in clean)
        {
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
        }
        return sb.ToString();
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
