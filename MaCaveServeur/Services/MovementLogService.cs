using System;
using System.Collections.Generic;
using System.Linq;

namespace MaCaveServeur.Services
{
    public sealed class MovementLogService
    {
        public sealed class MovementEntry
        {
            public Guid BottleId { get; set; }
            public string SiteCode { get; set; } = "";
            public string Action { get; set; } = "";
            public int Quantity { get; set; }
            public string? Note { get; set; }
            public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        }

        private readonly object _lock = new();
        private readonly List<MovementEntry> _entries = new();
        private const int MaxEntries = 5000;

        public void Add(MovementEntry entry)
        {
            lock (_lock)
            {
                _entries.Insert(0, entry);
                if (_entries.Count > MaxEntries)
                {
                    _entries.RemoveRange(MaxEntries, _entries.Count - MaxEntries);
                }
            }
        }

        public IReadOnlyList<MovementEntry> GetForBottle(Guid bottleId)
        {
            lock (_lock)
            {
                return _entries
                    .Where(e => e.BottleId == bottleId)
                    .OrderByDescending(e => e.TimestampUtc)
                    .ToList();
            }
        }
    }
}
