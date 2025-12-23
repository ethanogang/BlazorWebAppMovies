namespace MaCaveServeur.Services;

/// <summary>
/// Liste officielle des établissements (codes stables) et helpers de validation/normalisation.
/// </summary>
public static class Sites
{
    // Codes stockés en base et utilisés partout dans l'app.
    public static readonly string[] AllCodes = new[]
    {
        "BRUTUS",
        "BACCHUS",
        "MAILLARD",
        "GRAMMA",
        "MERLOT",
    };

    public static bool IsValid(string? siteCode)
    {
        if (string.IsNullOrWhiteSpace(siteCode)) return false;
        var c = siteCode.Trim();
        return AllCodes.Contains(c, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Retourne le code normalisé (uppercase) et garanti dans la liste, sinon lève une exception.</summary>
    public static string NormalizeOrThrow(string? siteCode)
    {
        if (!IsValid(siteCode))
            throw new ArgumentException($"Site invalide: '{siteCode ?? "(null)"}'.");

        return siteCode!.Trim().ToUpperInvariant();
    }

    /// <summary>Retourne le code normalisé (uppercase) ou null si vide.</summary>
    public static string? NormalizeOrNull(string? siteCode)
        => string.IsNullOrWhiteSpace(siteCode) ? null : siteCode.Trim().ToUpperInvariant();
}
