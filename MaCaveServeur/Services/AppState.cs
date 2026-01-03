using System;
using System.Collections.Generic;
using System.Linq;

namespace MaCaveServeur.Services
{
    /// <summary>
    /// Global application state: selected site/restaurant and list of sites.
    /// </summary>
    public sealed class AppState
    {
        public sealed class SiteOption
        {
            public string Code { get; set; } = "";
            public string Label { get; set; } = "";
        }

        private static readonly List<SiteOption> FixedSites = new()
        {
            new SiteOption { Code = "BRUTUS",   Label = "Brutus"   },
            new SiteOption { Code = "BACCHUS",  Label = "Bacchus"  },
            new SiteOption { Code = "MAILLARD", Label = "Maillard" },
            new SiteOption { Code = "GRAMMA",   Label = "Gramma"   },
            new SiteOption { Code = "MERLOT",   Label = "Merlot"   },
        };

        public List<SiteOption> AllSites { get; } = new()
        {
            new SiteOption { Code = "ALL", Label = "Maison Amour" },
        };

        public AppState()
        {
            AllSites.AddRange(FixedSites);
        }

        public string CurrentSiteCode { get; private set; } = "ALL";

        public string CurrentSiteName =>
            AllSites.FirstOrDefault(s => s.Code.Equals(CurrentSiteCode, StringComparison.OrdinalIgnoreCase))?.Label
            ?? "Maison Amour";

        public bool IsAll => CurrentSiteCode.Equals("ALL", StringComparison.OrdinalIgnoreCase);

        public void SetSite(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return;
            if (!AllSites.Any(s => s.Code.Equals(code, StringComparison.OrdinalIgnoreCase)))
                return;

            CurrentSiteCode = code.Trim().ToUpperInvariant();
            NotifyChanged();
        }

        public string CurrentSiteLabel => CurrentSiteName;
        public string SelectedSite => CurrentSiteCode;

        public string SelectedSiteCode
        {
            get => CurrentSiteCode;
            set => SetSite(value);
        }

        public event Action? Changed;

        private void NotifyChanged() => Changed?.Invoke();
    }
}
