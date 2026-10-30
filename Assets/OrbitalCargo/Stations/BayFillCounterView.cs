using TMPro;
using UnityEngine;

namespace OrbitalCargo.Stations
{
    /// <summary>World-space "4 / 6" above a bay (GDD: in-world fill counter). Turns green when full.</summary>
    [AddComponentMenu("OrbitalCargo/Stations/Bay Fill Counter View")]
    public sealed class BayFillCounterView : MonoBehaviour
    {
        [SerializeField] private Bay bay;
        [SerializeField] private TextMeshPro label;
        [SerializeField] private Color fillingColor = Color.white;
        [SerializeField] private Color fullColor = new Color(0.3f, 1f, 0.45f);

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (bay != null) bay.Changed += HandleBayChanged;
        }

        // Start, not OnEnable: the bay builds its model in Awake, which may run after our OnEnable.
        private void Start() => Refresh();

        private void OnDisable()
        {
            if (bay != null) bay.Changed -= HandleBayChanged;
        }

        // ── Handlers ───────────────────────────────────────────────────────────

        private void HandleBayChanged(Bay changed) => Refresh();

        private void Refresh()
        {
            if (bay == null || label == null || bay.Model == null) return;
            label.text = $"{bay.Model.Count} / {bay.Model.SlotCount}";
            label.color = bay.Model.IsFull ? fullColor : fillingColor;
        }
    }
}
