using OrbitalCargo.Config;
using UnityEngine;

namespace OrbitalCargo.Crates
{
    /// <summary>
    /// Applies a <see cref="CrateTypeSO"/>'s look to the crate (casing material, stripe colour) and owns
    /// the X-ray look: a see-through casing with the hidden item model showing inside. The stripe uses a
    /// property block so all crates share one stripe material.
    /// </summary>
    [AddComponentMenu("OrbitalCargo/Crates/Crate Visuals")]
    public sealed class CrateVisuals : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("Renderer of the crate body (Casing child).")]
        [SerializeField] private Renderer casing;
        [Tooltip("Renderer of the colour band (Stripe child).")]
        [SerializeField] private Renderer stripe;
        [Tooltip("Where a hidden item's model is spawned (HiddenItem child, crate centre).")]
        [SerializeField] private Transform hiddenAnchor;

        private MaterialPropertyBlock _block;
        private CrateTypeSO _type;
        private GameObject _hiddenModel;

        public Renderer Casing => casing;
        public bool IsXRay { get; private set; }

        /// <summary>Normal look for <paramref name="type"/>: opaque casing, stripe on, no hidden model.</summary>
        public void Apply(CrateTypeSO type)
        {
            if (type == null) return;
            _type = type;
            ClearHidden();
            SetXRay(false, null);

            if (stripe == null) return;
            _block ??= new MaterialPropertyBlock();
            stripe.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, type.AccentColor);
            stripe.SetPropertyBlock(_block);
        }

        /// <summary>See-through casing (<paramref name="xrayMaterial"/>) with the stripe hidden, or back to normal.</summary>
        public void SetXRay(bool on, Material xrayMaterial)
        {
            IsXRay = on && xrayMaterial != null;
            if (casing != null)
                casing.sharedMaterial = IsXRay ? xrayMaterial : (_type != null ? _type.CasingMaterial : casing.sharedMaterial);
            if (stripe != null) stripe.enabled = !IsXRay;
        }

        /// <summary>Spawn <paramref name="item"/>'s model inside the crate (once). No-op for a clean crate.</summary>
        public void ShowHidden(HiddenItemSO item)
        {
            if (item == null || item.Prefab == null || hiddenAnchor == null || _hiddenModel != null) return;
            _hiddenModel = Instantiate(item.Prefab, hiddenAnchor, false);
        }

        public void ClearHidden()
        {
            if (_hiddenModel == null) return;
            Destroy(_hiddenModel);
            _hiddenModel = null;
        }
    }
}
