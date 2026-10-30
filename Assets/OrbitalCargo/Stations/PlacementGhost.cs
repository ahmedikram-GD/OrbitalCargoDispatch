using UnityEngine;

namespace OrbitalCargo.Stations
{
    /// <summary>
    /// Translucent crate-sized box shown on the slot a crate would snap to, so placement is never a
    /// guess. Purely visual (no collider); the bay moves it and toggles its renderer.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    [AddComponentMenu("OrbitalCargo/Stations/Placement Ghost")]
    public sealed class PlacementGhost : MonoBehaviour
    {
        [Tooltip("Height (m) of the ghost's centre above the slot anchor — half a crate.")]
        [SerializeField] private float centreHeight = 0.4f;

        private Renderer _renderer;

        public bool IsVisible => _renderer != null && _renderer.enabled;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _renderer.enabled = false;
        }

        public void Show(Transform anchor)
        {
            if (_renderer == null) return;
            transform.SetPositionAndRotation(anchor.position + anchor.up * centreHeight, anchor.rotation);
            _renderer.enabled = true;
        }

        public void Hide()
        {
            if (_renderer != null) _renderer.enabled = false;
        }
    }
}
