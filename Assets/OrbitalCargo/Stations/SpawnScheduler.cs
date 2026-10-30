using System.Collections.Generic;
using OrbitalCargo.Config;

namespace OrbitalCargo.Stations
{
    /// <summary>
    /// Pure expansion of a spawn timeline: each <see cref="SpawnEntry"/> becomes Count individual
    /// <see cref="SpawnRequest"/>s Interval apart, released in time order exactly once as the shift
    /// clock passes them. Whether the belt has room is the spawner's concern, not this one's.
    /// </summary>
    public sealed class SpawnScheduler
    {
        private readonly List<SpawnRequest> _requests = new List<SpawnRequest>();
        private int _next;

        public SpawnScheduler(IReadOnlyList<SpawnEntry> entries)
        {
            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    if (entry == null || entry.Type == null) continue;
                    for (int i = 0; i < entry.Count; i++)
                    {
                        _requests.Add(new SpawnRequest
                        {
                            Time = entry.Time + i * entry.Interval,
                            Type = entry.Type,
                            Suspicious = entry.Suspicious,
                            Hidden = entry.Hidden,
                            RadioBeat = i == 0 ? entry.RadioBeat : null,
                        });
                    }
                }
            }
            // Stable sort by time (List.Sort is unstable, so break ties by original order).
            var indexed = new List<(SpawnRequest req, int order)>(_requests.Count);
            for (int i = 0; i < _requests.Count; i++) indexed.Add((_requests[i], i));
            indexed.Sort((a, b) => a.req.Time != b.req.Time ? a.req.Time.CompareTo(b.req.Time) : a.order.CompareTo(b.order));
            _requests.Clear();
            foreach (var item in indexed) _requests.Add(item.req);
        }

        public bool IsExhausted => _next >= _requests.Count;
        public int Total => _requests.Count;

        /// <summary>Append every not-yet-released spawn with Time ≤ <paramref name="elapsed"/> to <paramref name="due"/>.</summary>
        public void Advance(float elapsed, List<SpawnRequest> due)
        {
            while (_next < _requests.Count && _requests[_next].Time <= elapsed)
                due.Add(_requests[_next++]);
        }
    }
}
