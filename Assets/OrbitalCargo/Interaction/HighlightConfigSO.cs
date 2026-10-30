using UnityEngine;

namespace OrbitalCargo.Interaction
{
    /// <summary>How the outline reads against depth.</summary>
    public enum OutlineMode
    {
        /// <summary>Always visible, even through walls (crisp selection cue).</summary>
        OutlineAll,
        /// <summary>Only the parts of the outline that aren't occluded.</summary>
        OutlineVisible,
        /// <summary>Only the parts hidden behind other geometry (x-ray).</summary>
        OutlineHidden,
    }

    /// <summary>
    /// Look of the focus outline, shared by every crate: the mask+fill template materials, colour,
    /// screen-space width, depth mode and an optional pulse. Ported from Silly-Justice (FP_HighlightConfig).
    /// </summary>
    [CreateAssetMenu(fileName = "FP_HighlightConfig", menuName = "OrbitalCargo/Interaction/Highlight Config")]
    public class HighlightConfigSO : ScriptableObject
    {
        [Header("Materials (assign once here, not per item)")]
        [Tooltip("Template material using FlipFurry/FirstPersonOutlineMask (FP_OutlineMask). Instanced per item.")]
        public Material MaskMaterial;
        [Tooltip("Template material using FlipFurry/FirstPersonOutlineFill (FP_OutlineFill). Instanced per item.")]
        public Material FillMaterial;

        [Header("Outline")]
        [ColorUsage(true, true)] public Color OutlineColor = new Color(0.2f, 0.9f, 1f, 1f);
        [Tooltip("Outline thickness in screen space (constant at any distance). ~2–8 reads well.")]
        [Range(0f, 10f)] public float OutlineWidth = 4f;
        [Tooltip("Depth behaviour. OutlineVisible hides the outline behind walls.")]
        public OutlineMode Mode = OutlineMode.OutlineAll;

        [Header("Pulse")]
        [Tooltip("Breathe the outline width so it reads as interactive.")]
        public bool Pulse = true;
        [Min(0f)] public float PulseSpeed = 4.5f;
        [Tooltip("Fraction the width grows/shrinks over the pulse (0.3 = ±30%).")]
        [Range(0f, 1f)] public float PulseAmount = 0.25f;
    }
}
