using System.Collections.Generic;
using UnityEngine;

namespace OrbitalCargo.Stations
{
    /// <summary>
    /// Pure model of a conveyor belt: crates (by id) ride a waypoint path at a speed, keep a minimum
    /// spacing, queue up at the end, and close the gap when one is taken. No physics (KICKOFF rule) —
    /// the <c>Conveyor</c> MonoBehaviour just writes these positions onto the crates.
    /// </summary>
    public sealed class ConveyorQueue
    {
        private const float EndEpsilon = 1e-3f;

        private readonly Vector3[] _points;
        private readonly float[] _cumulative; // distance along the path at each waypoint
        private readonly List<int> _ids = new List<int>();          // front (furthest along) first
        private readonly List<float> _distances = new List<float>(); // parallel to _ids

        public ConveyorQueue(IReadOnlyList<Vector3> waypoints, float spacing)
        {
            _points = new Vector3[waypoints.Count];
            _cumulative = new float[waypoints.Count];
            for (int i = 0; i < waypoints.Count; i++)
            {
                _points[i] = waypoints[i];
                if (i > 0) _cumulative[i] = _cumulative[i - 1] + Vector3.Distance(waypoints[i - 1], waypoints[i]);
            }
            Length = _points.Length > 0 ? _cumulative[_cumulative.Length - 1] : 0f;
            Spacing = Mathf.Max(0f, spacing);
        }

        public float Length { get; }
        public float Spacing { get; }
        public int Count => _ids.Count;

        /// <summary>True when the entry is clear: the last crate on the belt has moved at least one spacing in.</summary>
        public bool CanEnqueue => _ids.Count == 0 || _distances[_distances.Count - 1] >= Spacing;

        /// <summary>Ids front-first (the crate nearest the end of the belt comes first).</summary>
        public IEnumerable<int> Items => _ids;

        public bool Enqueue(int id)
        {
            if (!CanEnqueue || _ids.Contains(id)) return false;
            _ids.Add(id);
            _distances.Add(0f);
            return true;
        }

        public bool Remove(int id)
        {
            int index = _ids.IndexOf(id);
            if (index < 0) return false;
            _ids.RemoveAt(index);
            _distances.RemoveAt(index);
            return true;
        }

        public bool Contains(int id) => _ids.Contains(id);

        /// <summary>Move every crate forward; each stops at the end or one spacing behind the crate ahead.</summary>
        public void Advance(float dt, float speed)
        {
            float step = Mathf.Max(0f, dt * speed);
            for (int i = 0; i < _distances.Count; i++)
            {
                float limit = i == 0 ? Length : _distances[i - 1] - Spacing;
                float next = Mathf.Min(_distances[i] + step, limit);
                _distances[i] = Mathf.Max(_distances[i], next); // never slide backwards
            }
        }

        public float DistanceOf(int id)
        {
            int index = _ids.IndexOf(id);
            return index >= 0 ? _distances[index] : 0f;
        }

        public Vector3 PositionOf(int id)
        {
            float d = DistanceOf(id);
            int leg = LegAt(d);
            if (leg < 0) return _points.Length > 0 ? _points[0] : Vector3.zero;
            float legLength = _cumulative[leg + 1] - _cumulative[leg];
            float t = legLength > 0f ? (d - _cumulative[leg]) / legLength : 0f;
            return Vector3.Lerp(_points[leg], _points[leg + 1], t);
        }

        public Vector3 ForwardOf(int id)
        {
            int leg = LegAt(DistanceOf(id));
            if (leg < 0) return Vector3.forward;
            Vector3 dir = _points[leg + 1] - _points[leg];
            return dir.sqrMagnitude > 0f ? dir.normalized : Vector3.forward;
        }

        public bool IsAtEnd(int id) => Contains(id) && DistanceOf(id) >= Length - EndEpsilon;

        // ── Helpers ────────────────────────────────────────────────────────────

        /// <summary>Index of the leg (waypoint i → i+1) containing distance <paramref name="d"/>; -1 if the path has no legs.</summary>
        private int LegAt(float d)
        {
            if (_points.Length < 2) return -1;
            for (int i = 0; i < _points.Length - 2; i++)
                if (d < _cumulative[i + 1]) return i;
            return _points.Length - 2;
        }
    }
}
