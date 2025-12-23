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
        // Codes "fixes" (ceux qui existent réellement).
        private static readonly List<SiteOption> _fixedSites = new()
        {
            new SiteOption { Code = "BRUTUS",   Label = "Brutus"   },
            new SiteOption { Code = "BACCHUS",  Label = "Bacchus"  },
            new SiteOption { Code = "MAILLARD", Label = "Maillard" },
            new SiteOption { Code = "GRAMMA",   Label = "Gramma"   },
            new SiteOption { Code = "MERLOT",   Label = "Merlot"   },
        };

        // Inclut l'option "ALL" côté UI (jamais stockée dans les bouteilles)
        public List<SiteOption> AllSites { get; } = new()
        {
            new SiteOption { Code = "ALL", Label = "Groupe Amour (tous)" },
        };

        public AppState()
        {
            AllSites.AddRange(_fixedSites);
        }

        // --- Site courant (code) ---
        public string CurrentSiteCode { get; private set; } = "ALL";

        // --- Nom lisible du site courant ---
        public string CurrentSiteName =>
            AllSites.FirstOrDefault(s => s.Code.Equals(CurrentSiteCode, StringComparison.OrdinalIgnoreCase))?.Label
            ?? "Groupe Amour";

        // --- Mode groupe ---
        public bool IsAll => CurrentSiteCode.Equals("ALL", StringComparison.OrdinalIgnoreCase);

        public void SetSite(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return;
            if (!AllSites.Any(s => s.Code.Equals(code, StringComparison.OrdinalIgnoreCase)))
                return;

            CurrentSiteCode = code.Trim().ToUpperInvariant();
            NotifyChanged();
        }

        // ------------------------------------------------------------------
        //                 ALIAS RÉTRO-COMPATIBILITÉ
        // ------------------------------------------------------------------

        /// <summary>Ancien alias utilisé par certaines pages : CurrentSiteName.</summary>
        public string CurrentSiteLabel => CurrentSiteName;

        /// <summary>Ancien alias utilisé par certaines pages : CurrentSiteCode.</summary>
        public string SelectedSite => CurrentSiteCode;

        /// <summary>Ancien setter sous forme de propriété (certaines pages faisaient State.SelectedSiteCode = …).</summary>
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
