using MaCaveServeur.Models;

namespace MaCaveServeur.Services;

public static class BottleNormalization
{
    /// <summary>
    /// Normalise les champs texte pour éviter les doublons dus aux espaces/casse.
    /// </summary>
    public static void NormalizeInPlace(Bottle b)
    {
        // Site code : codes stables
        b.SiteCode = Sites.NormalizeOrNull(b.SiteCode);

        // Textes : trim + espaces multiples + null si vide
        b.ProductReferenceCode = CleanText(b.ProductReferenceCode);
        b.Producer = CleanText(b.Producer);
        b.Name = CleanText(b.Name);
        b.Designation = CleanText(b.Designation);
        b.Region = CleanText(b.Region);
        b.Appellation = CleanText(b.Appellation);
        b.Supplier = CleanText(b.Supplier);
        b.Country = CleanText(b.Country);
        b.Grapes = CleanText(b.Grapes);
        b.Color = CleanText(b.Color);
        b.ExternalId = CleanText(b.ExternalId);
        b.GlassExternalId = CleanText(b.GlassExternalId);

        if (b.Vintage < 0) b.Vintage = 0;

        // Stock : jamais négatif
        if (b.Quantity < 0) b.Quantity = 0;
        if (b.LowStockThreshold < 0) b.LowStockThreshold = 0;
    }

    /// <summary>
    /// Clé métier utilisée pour détecter un doublon.
    /// Choix volontairement simple et stable.
    /// </summary>
    public static string BusinessKey(Bottle b)
    {
        var site = (b.SiteCode ?? "").Trim().ToUpperInvariant();
        var producer = Canon(b.Producer);
        var name = Canon(b.Name);
        var supplier = Canon(b.Supplier);

        return $"{site}||{producer}||{name}||{b.Vintage}||{supplier}";
    }

    public static string? CleanText(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();

        // Remplace séquences d'espaces par un seul espace
        var parts = s.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", parts);
    }

    private static string Canon(string? s)
        => (CleanText(s) ?? "").ToUpperInvariant();
}
