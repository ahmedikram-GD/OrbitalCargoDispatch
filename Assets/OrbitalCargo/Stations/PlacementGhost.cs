using UnityEngine;

namespace OrbitalCargo.Stations
{
    /// <summary>
    /// Translucent crate-sized box shown on the slot a crate would snap to, so placement is never a
    /// guess. Turns red when the carried crate doesn't belong in this bay — the warning lands before
    /// the mistake, because a placed crate can't be taken back out. Purely visual (no collider); the bay
    /// moves it and toggles its renderer.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    [AddComponentMenu("OrbitalCargo/Stations/Placement Ghost")]
    public sealed class PlacementGhost : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("Height (m) of the ghost's centre above the slot anchor — half a crate.")]
        [SerializeField] private float centreHeight = 0.4f;
        [Tooltip("Ghost tint when the crate is aimed at the wrong bay (keep it translucent).")]
        [SerializeField] private Color wrongColor = new Color(1f, 0.15f, 0.1f, 0.5f);

        private Renderer _renderer;
        private MaterialPropertyBlock _block;
        private Color _okColor = Color.white;

        public bool IsVisible => _renderer != null && _renderer.enabled;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _renderer.enabled = false;
            _block = new MaterialPropertyBlock();
            var material = _renderer.sharedMaterial;
            if (material != null && material.HasProperty(BaseColorId)) _okColor = material.GetColor(BaseColorId);
        }

        public void Show(Transform anchor, bool wrongBay = false)
        {
            if (_renderer == null) return;
            transform.SetPositionAndRotation(anchor.position + anchor.up * centreHeight, anchor.rotation);
            _renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, wrongBay ? wrongColor : _okColor);
            _renderer.SetPropertyBlock(_block);
            _renderer.enabled = true;
        }

        public void Hide()
        {
            if (_renderer != null) _renderer.enabled = false;
        }
    }
}
