using System;
using System.Collections.Generic;
using System.Linq;

namespace MaCaveServeur.Services
{
    /// <summary>
    /// État applicatif global : site/établissement sélectionné, liste des sites,
    /// et quelques alias pour la compatibilité avec l’ancien code.
    /// </summary>
    public sealed class AppState
    {
        // --- Modèle d’un site/établissement ---
        public sealed class SiteOption
        {
            public string Code { get; set; } = "";   // ex: "ALL", "BRUTUS"
            public string Label { get; set; } = "";  // ex: "Groupe Amour (tous)", "Brutus"
        }

        // --- Liste des établissements disponibles ---
        // Ajoute/retire ici selon ton besoin.
        public List<SiteOption> AllSites { get; } = new()
        {
            new SiteOption { Code = "ALL",     Label = "Groupe Amour (tous)" },
            new SiteOption { Code = "BRUTUS",  Label = "Brutus" },
            new SiteOption { Code = "GRAMMA",  Label = "Gramma" },
            new SiteOption { Code = "BACCHUS", Label = "Bacchus" },
        };

        // --- Site courant (code) ---
        public string CurrentSiteCode { get; private set; } = "ALL";

        // --- Nom lisible du site courant ---
        public string CurrentSiteName =>
            AllSites.FirstOrDefault(s => s.Code == CurrentSiteCode)?.Label
            ?? "Groupe Amour";

        // --- Sélection d’un site ---
        public void SetSite(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return;
            if (!AllSites.Any(s => s.Code.Equals(code, StringComparison.OrdinalIgnoreCase)))
                return;

            CurrentSiteCode = code.ToUpperInvariant();
            NotifyChanged();
        }

        // ------------------------------------------------------------------
        //                 ALIAS RÉTRO-COMPATIBILITÉ
        // ------------------------------------------------------------------

        /// <summary>Ancien nom utilisé par certaines pages (équivaut à AllSites).</summary>
        public IEnumerable<SiteOption> Sites => AllSites;

        /// <summary>Ancien nom : code du site sélectionné (string).</summary>
        public string SelectedSite => CurrentSiteCode;

        /// <summary>Ancien booléen : vrai si on est en vue agrégée.</summary>
        public bool IsAll => string.Equals(CurrentSiteCode, "ALL", StringComparison.OrdinalIgnoreCase);

        /// <summary>Ancien setter sous forme de propriété (certaines pages faisaient State.SelectedSite = …).</summary>
        public string SelectedSiteCode
        {
            get => CurrentSiteCode;
            set => SetSite(value);
        }

        // ------------------------------------------------------------------
        //        Notification (si tu veux rafraîchir automatiquement l’UI)
        // ------------------------------------------------------------------
        public event Action? Changed;

        private void NotifyChanged() => Changed?.Invoke();
    }
}
