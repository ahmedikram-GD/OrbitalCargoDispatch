using OrbitalCargo.Interaction;
using UnityEngine;

namespace OrbitalCargo.Stations
{
    /// <summary>
    /// The green launch pedestal beside each bay. Always offers "Launch" (so the player learns where it
    /// is) but is only enabled when the bay is full; its lamp glows and pulses while ready. Hidden while
    /// the shuttle is away.
    /// </summary>
    [AddComponentMenu("OrbitalCargo/Stations/Launch Button")]
    public sealed class LaunchButton : MonoBehaviour, IInteractable
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private ShuttleLauncher launcher;
        [Tooltip("FP_InteractionConfig — prompt text.")]
        [SerializeField] private InteractionConfigSO config;
        [Tooltip("Lamp renderer on top of the pedestal (material must have emission enabled).")]
        [SerializeField] private Renderer lamp;

        [Header("Lamp")]
        [ColorUsage(false, true)] [SerializeField] private Color readyEmission = new Color(0.2f, 1f, 0.4f) * 3f;
        [ColorUsage(false, true)] [SerializeField] private Color idleEmission = new Color(0.2f, 1f, 0.4f) * 0.15f;
        [Tooltip("Pulses per second while ready.")]
        [Min(0f)] [SerializeField] private float pulseSpeed = 3f;

        private MaterialPropertyBlock _block;

        public ShuttleLauncher Launcher => launcher;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Update()
        {
            if (lamp == null || launcher == null) return;
            _block ??= new MaterialPropertyBlock();

            Color emission = idleEmission;
            if (launcher.CanLaunch)
            {
                float pulse = 0.6f + 0.4f * Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f);
                emission = readyEmission * pulse;
            }
            lamp.GetPropertyBlock(_block);
            _block.SetColor(EmissionColorId, emission);
            lamp.SetPropertyBlock(_block);
        }

        // ── IInteractable ──────────────────────────────────────────────────────

        public string Prompt(CrateCarrier carrier) =>
            launcher == null || launcher.IsLaunching || config == null ? string.Empty : config.LaunchPrompt;

        public bool CanInteract(CrateCarrier carrier) => launcher != null && launcher.CanLaunch;

        public void Interact(CrateCarrier carrier, RaycastHit hit)
        {
            if (launcher != null) launcher.TryLaunch();
        }
    }
}
