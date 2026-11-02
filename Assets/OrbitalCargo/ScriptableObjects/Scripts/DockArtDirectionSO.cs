using UnityEngine;

namespace OrbitalCargo.Config
{
    /// <summary>Shared palette and scene-light tuning for the reproducible first dock art pass.</summary>
    [CreateAssetMenu(fileName = "DockArtDirection", menuName = "OrbitalCargo/Room/Dock Art Direction")]
    public sealed class DockArtDirectionSO : ScriptableObject
    {
        public bool Enabled = true;
        public Color Wall = new Color(0.11f, 0.17f, 0.24f);
        public Color Panel = new Color(0.18f, 0.25f, 0.32f);
        public Color Floor = new Color(0.25f, 0.30f, 0.35f);
        public Color Frame = new Color(0.07f, 0.10f, 0.14f);
        public Color Metal = new Color(0.32f, 0.40f, 0.47f);
        public Color Amber = new Color(1f, 0.64f, 0.16f);
        public Color Cyan = new Color(0.12f, 0.85f, 1f);
        public Color Label = new Color(0.87f, 0.93f, 0.96f);
        [Range(0f, 2f)] public float KeyIntensity = 0.65f;
        [Range(0f, 10f)] public float FixtureIntensity = 4f;
        [Range(0f, 3f)] public float Emission = 1.4f;
        public bool ContactShadows = true;
        [Range(0f, 1f)] public float ShadowStrength = 0.65f;
        [Header("Authored environment benchmark")]
        public bool UseEnvironmentKit = true;
        [Range(0f, 15f)] public float CornerBakedLightIntensity = 5f;
        [Range(0f, 10f)] public float CornerRealtimeFill = 2f;
    }
}
