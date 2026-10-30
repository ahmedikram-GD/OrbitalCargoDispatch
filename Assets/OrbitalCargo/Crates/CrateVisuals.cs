using OrbitalCargo.Config;
using UnityEngine;

namespace OrbitalCargo.Crates
{
    /// <summary>
    /// Applies a <see cref="CrateTypeSO"/>'s look to the crate: casing material and stripe colour.
    /// The stripe uses a property block so all crates share one stripe material.
    /// </summary>
    [AddComponentMenu("OrbitalCargo/Crates/Crate Visuals")]
    public sealed class CrateVisuals : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("Renderer of the crate body (Casing child).")]
        [SerializeField] private Renderer casing;
        [Tooltip("Renderer of the colour band (Stripe child).")]
        [SerializeField] private Renderer stripe;

        private MaterialPropertyBlock _block;

        public Renderer Casing => casing;

        public void Apply(CrateTypeSO type)
        {
            if (type == null) return;
            if (casing != null && type.CasingMaterial != null) casing.sharedMaterial = type.CasingMaterial;
            if (stripe == null) return;

            _block ??= new MaterialPropertyBlock();
            stripe.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, type.AccentColor);
            stripe.SetPropertyBlock(_block);
        }
    }
}
