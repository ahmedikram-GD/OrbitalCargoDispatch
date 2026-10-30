using GameKit.Architecture.PoolSystem;
using GameKit.Haptics;
using OrbitalCargo.Crates;
using OrbitalCargo.Economy;
using OrbitalCargo.FirstPerson;
using THEBADDEST.SoundSystem;
using UnityEngine;

namespace OrbitalCargo.Juice
{
    /// <summary>
    /// The one place gameplay events become feedback (the kit's "presenter" role): sounds, haptics,
    /// particles, camera shake and camera kicks. Gameplay code only raises events; this listens.
    /// Slice 3 wires pick-up, place and launch; later slices add their own mappings here.
    /// </summary>
    [AddComponentMenu("OrbitalCargo/Juice/Juice Presenter")]
    public sealed class JuicePresenter : MonoBehaviour
    {
        private const string LaunchSmokePoolId = "LaunchSmoke";

        [Header("Services")]
        [SerializeField] private SoundService sound;
        [SerializeField] private HapticHandlerSO haptics;
        [Tooltip("ParticlesPooler — holds LaunchSmoke.")]
        [SerializeField] private PoolManagerSO particles;

        [Header("Camera (scene, set by SceneSetup)")]
        [SerializeField] private CameraShaker shaker;
        [SerializeField] private CameraBobController bob;

        [Header("Events")]
        [SerializeField] private GameEventWithCrate onCratePicked;
        [SerializeField] private GameEventWithCrate onCratePlaced;
        [SerializeField] private GameEventWithLaunchResult onShuttleLaunched;
        [SerializeField] private GameEventWithCrate onCryoExpired;
        [SerializeField] private GameEventWithCrate onCrateThrown;
        [SerializeField] private GameEventWithCrate onCrateChuted;

        [Header("Feel")]
        [SerializeField] private CameraShakeProfileSO launchShake;
        [Tooltip("Camera kick (m) when a crate lands in the hands.")]
        [Min(0f)] [SerializeField] private float pickKickAmplitude = 0.02f;
        [Min(0f)] [SerializeField] private float pickKickDuration = 0.15f;
        [Tooltip("Seconds before a launch smoke puff returns to the pool.")]
        [Min(0.1f)] [SerializeField] private float smokeLifetime = 3f;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (onCratePicked != null) onCratePicked.Subscribe(HandleCratePicked);
            if (onCratePlaced != null) onCratePlaced.Subscribe(HandleCratePlaced);
            if (onShuttleLaunched != null) onShuttleLaunched.Subscribe(HandleShuttleLaunched);
            if (onCryoExpired != null) onCryoExpired.Subscribe(HandleCryoExpired);
            if (onCrateThrown != null) onCrateThrown.Subscribe(HandleCrateThrown);
            if (onCrateChuted != null) onCrateChuted.Subscribe(HandleCrateChuted);
        }

        private void OnDisable()
        {
            if (onCratePicked != null) onCratePicked.UnSubscribe(HandleCratePicked);
            if (onCratePlaced != null) onCratePlaced.UnSubscribe(HandleCratePlaced);
            if (onShuttleLaunched != null) onShuttleLaunched.UnSubscribe(HandleShuttleLaunched);
            if (onCryoExpired != null) onCryoExpired.UnSubscribe(HandleCryoExpired);
            if (onCrateThrown != null) onCrateThrown.UnSubscribe(HandleCrateThrown);
            if (onCrateChuted != null) onCrateChuted.UnSubscribe(HandleCrateChuted);
        }

        // ── Handlers ───────────────────────────────────────────────────────────

        private void HandleCratePicked(Crate crate)
        {
            if (sound != null) sound.Play("Pickup");
            if (haptics != null) haptics.ApplyHapticFeedback(HapticIntensity.Light);
            if (bob != null) bob.Kick(pickKickAmplitude, pickKickDuration);
        }

        private void HandleCratePlaced(Crate crate)
        {
            if (sound != null) sound.Play("Place");
            if (haptics != null) haptics.ApplyHapticFeedback(HapticIntensity.Medium);
        }

        private void HandleCryoExpired(Crate crate)
        {
            if (haptics != null) haptics.ApplyHapticFeedback(HapticIntensity.Failure);
        }

        private void HandleCrateThrown(Crate crate)
        {
            if (haptics != null) haptics.ApplyHapticFeedback(HapticIntensity.Light);
        }

        private void HandleCrateChuted(Crate crate)
        {
            if (sound != null) sound.Play("CoinsCollect");
            if (haptics != null) haptics.ApplyHapticFeedback(HapticIntensity.Medium);
        }

        private void HandleShuttleLaunched(LaunchResult result)
        {
            if (sound != null) sound.Play("Launch");
            if (haptics != null) haptics.ApplyHapticFeedback(HapticIntensity.Success);
            if (shaker != null) shaker.Play(launchShake);

            if (particles == null) return;
            GameObject smoke = particles.Get(LaunchSmokePoolId);
            if (smoke == null) return;
            smoke.transform.position = result.WorldPosition;
            var ps = smoke.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Clear(true);
                ps.Play(true);
            }
            particles.ReleaseAfterDelay(LaunchSmokePoolId, smoke, smokeLifetime);
        }
    }
}
