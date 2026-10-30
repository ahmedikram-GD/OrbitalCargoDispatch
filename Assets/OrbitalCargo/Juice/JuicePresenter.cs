using System.Collections;
using GameKit.Architecture.Events;
using GameKit.Architecture.PoolSystem;
using GameKit.Haptics;
using OrbitalCargo.Config;
using OrbitalCargo.Crates;
using OrbitalCargo.Economy;
using OrbitalCargo.FirstPerson;
using OrbitalCargo.Radio;
using THEBADDEST.SoundSystem;
using UnityEngine;

namespace OrbitalCargo.Juice
{
    /// <summary>
    /// The one place gameplay events become feedback (the kit's "presenter" role): sounds, haptics,
    /// particles, world pop-ups, prop wobbles, camera shake and camera kicks. Gameplay code only raises
    /// events; this listens. Covers the crate loop (pick, place, throw, chute, scan, cryo), launches,
    /// news, the shift flow buttons and Rook's radio blip.
    /// </summary>
    [AddComponentMenu("OrbitalCargo/Juice/Juice Presenter")]
    public sealed class JuicePresenter : MonoBehaviour
    {
        private const string LaunchSmokePoolId = "LaunchSmoke";
        private const string PopupPoolId = "Popup";

        [Header("Services")]
        [SerializeField] private SoundService sound;
        [SerializeField] private HapticHandlerSO haptics;
        [Tooltip("ParticlesPooler — holds LaunchSmoke and Popup.")]
        [SerializeField] private PoolManagerSO particles;
        [Tooltip("ShiftConfig_Pitch — the chute pop-up shows what PayoutRules.Chute paid.")]
        [SerializeField] private ShiftConfigSO config;
        [Tooltip("RadioScript_Pitch — the blip plays only for beats Rook actually has a line for.")]
        [SerializeField] private RadioScriptSO radioScript;

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
        [SerializeField] private GameEventWithCrate onScanStarted;
        [SerializeField] private GameEventWithCrate onScanResult;
        [SerializeField] private GameEventWithHiddenItem onItemCollected;
        [SerializeField] private GameEventWithString onNewsAlert;
        [SerializeField] private GameEventWithString onRadioBeat;

        [Header("Flow events")]
        [Tooltip("e_PausePressed, e_ResumePressed, e_NextShiftPressed — UI click + selection tick.")]
        [SerializeField] private GameEvent[] onUiPressed;
        [Tooltip("e_DoublePayoutClaimed, e_UpgradePurchased — cash register.")]
        [SerializeField] private GameEvent[] onPurchase;
        [SerializeField] private GameEvent onShiftEnded;

        [Header("Feel")]
        [SerializeField] private CameraShakeProfileSO launchShake;
        [Tooltip("Camera kick (m) when a crate lands in the hands.")]
        [Min(0f)] [SerializeField] private float pickKickAmplitude = 0.02f;
        [Min(0f)] [SerializeField] private float pickKickDuration = 0.15f;
        [Tooltip("Seconds before a launch smoke puff returns to the pool.")]
        [Min(0.1f)] [SerializeField] private float smokeLifetime = 3f;
        [Tooltip("Longest a placed crate is waited on to finish its snap before the wobble is skipped.")]
        [Min(0.1f)] [SerializeField] private float landingTimeout = 1f;
        [Tooltip("Metres above a crate (or the chute mouth) that a pop-up starts.")]
        [Min(0f)] [SerializeField] private float popupHeight = 0.9f;

        [Header("Pop-up colours")]
        [SerializeField] private Color moneyColor = new Color(1f, 0.85f, 0.25f);
        [SerializeField] private Color badColor = new Color(1f, 0.25f, 0.2f);
        [SerializeField] private Color goodColor = new Color(0.4f, 1f, 0.5f);
        [SerializeField] private Color coldColor = new Color(0.5f, 0.85f, 1f);

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (onCratePicked != null) onCratePicked.Subscribe(HandleCratePicked);
            if (onCratePlaced != null) onCratePlaced.Subscribe(HandleCratePlaced);
            if (onShuttleLaunched != null) onShuttleLaunched.Subscribe(HandleShuttleLaunched);
            if (onCryoExpired != null) onCryoExpired.Subscribe(HandleCryoExpired);
            if (onCrateThrown != null) onCrateThrown.Subscribe(HandleCrateThrown);
            if (onCrateChuted != null) onCrateChuted.Subscribe(HandleCrateChuted);
            if (onScanStarted != null) onScanStarted.Subscribe(HandleScanStarted);
            if (onScanResult != null) onScanResult.Subscribe(HandleScanResult);
            if (onItemCollected != null) onItemCollected.Subscribe(HandleItemCollected);
            if (onNewsAlert != null) onNewsAlert.Subscribe(HandleNewsAlert);
            if (onRadioBeat != null) onRadioBeat.Subscribe(HandleRadioBeat);
            if (onShiftEnded != null) onShiftEnded.Subscribe(HandleShiftEnded);
            if (onUiPressed != null) foreach (var e in onUiPressed) if (e != null) e.Subscribe(HandleUiPressed);
            if (onPurchase != null) foreach (var e in onPurchase) if (e != null) e.Subscribe(HandlePurchase);
        }

        private void OnDisable()
        {
            if (onCratePicked != null) onCratePicked.UnSubscribe(HandleCratePicked);
            if (onCratePlaced != null) onCratePlaced.UnSubscribe(HandleCratePlaced);
            if (onShuttleLaunched != null) onShuttleLaunched.UnSubscribe(HandleShuttleLaunched);
            if (onCryoExpired != null) onCryoExpired.UnSubscribe(HandleCryoExpired);
            if (onCrateThrown != null) onCrateThrown.UnSubscribe(HandleCrateThrown);
            if (onCrateChuted != null) onCrateChuted.UnSubscribe(HandleCrateChuted);
            if (onScanStarted != null) onScanStarted.UnSubscribe(HandleScanStarted);
            if (onScanResult != null) onScanResult.UnSubscribe(HandleScanResult);
            if (onItemCollected != null) onItemCollected.UnSubscribe(HandleItemCollected);
            if (onNewsAlert != null) onNewsAlert.UnSubscribe(HandleNewsAlert);
            if (onRadioBeat != null) onRadioBeat.UnSubscribe(HandleRadioBeat);
            if (onShiftEnded != null) onShiftEnded.UnSubscribe(HandleShiftEnded);
            if (onUiPressed != null) foreach (var e in onUiPressed) if (e != null) e.UnSubscribe(HandleUiPressed);
            if (onPurchase != null) foreach (var e in onPurchase) if (e != null) e.UnSubscribe(HandlePurchase);
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
            if (crate == null) return;

            // e_CratePlaced fires as the snap starts. Wobbling mid-snap would make the punch tween restore
            // a pre-landing pose, so wait for the crate to actually land (frame-rate independent).
            StartCoroutine(WobbleWhenLanded(crate));
        }

        private void HandleCryoExpired(Crate crate)
        {
            if (sound != null) sound.Play("CryoBeep");
            if (haptics != null) haptics.ApplyHapticFeedback(HapticIntensity.Failure);
            if (crate != null) Popup("THAWED", coldColor, crate.transform.position);
        }

        private void HandleCrateThrown(Crate crate)
        {
            if (sound != null) sound.Play("Throw");
            if (haptics != null) haptics.ApplyHapticFeedback(HapticIntensity.Light);
        }

        private void HandleCrateChuted(Crate crate)
        {
            if (sound != null) sound.Play("CoinsCollect");
            if (haptics != null) haptics.ApplyHapticFeedback(HapticIntensity.Medium);
            if (crate == null || config == null) return;

            int credits = PayoutRules.Chute(CrateSnapshot.Of(crate), config).Credits;
            if (credits > 0) Popup($"+{credits} ¢", moneyColor, crate.transform.position);
            // A clean crate down the chute is cargo thrown away (GDD); junk and contraband stay quiet.
            else if (!crate.IsBad && (crate.Type == null || !crate.Type.IsJunk)) Popup("LOST", badColor, crate.transform.position);
        }

        private void HandleScanStarted(Crate crate)
        {
            if (sound != null) sound.Play("Click");
            if (haptics != null) haptics.ApplyHapticFeedback(HapticIntensity.Selection);
        }

        private void HandleScanResult(Crate crate)
        {
            bool bad = crate != null && crate.IsBad;
            if (sound != null) sound.Play(bad ? "ScanBad" : "ScanGood");
            if (haptics != null) haptics.ApplyHapticFeedback(bad ? HapticIntensity.Failure : HapticIntensity.Success);
            if (bad && shaker != null) shaker.Play(launchShake);
            if (crate != null) Popup(bad ? "CONTRABAND!" : "CLEAN", bad ? badColor : goodColor, crate.transform.position);
        }

        private void HandleItemCollected(Config.HiddenItemSO item)
        {
            if (haptics != null) haptics.ApplyHapticFeedback(HapticIntensity.Success);
        }

        private void HandleNewsAlert(string line)
        {
            if (sound != null) sound.Play("Alarm");
            if (haptics != null) haptics.ApplyHapticFeedback(HapticIntensity.Failure);
        }

        private void HandleRadioBeat(string beat)
        {
            if (radioScript != null && !radioScript.TryGet(beat, out _)) return;
            if (sound != null) sound.Play("RadioBlip");
            if (haptics != null) haptics.ApplyHapticFeedback(HapticIntensity.Selection);
        }

        private void HandleShiftEnded()
        {
            if (sound != null) sound.Play("ShiftEnd");
            if (haptics != null) haptics.ApplyHapticFeedback(HapticIntensity.Success);
        }

        private void HandleUiPressed()
        {
            if (sound != null) sound.Play("Click");
            if (haptics != null) haptics.ApplyHapticFeedback(HapticIntensity.Selection);
        }

        private void HandlePurchase()
        {
            if (sound != null) sound.Play("CoinsCollect");
            if (haptics != null) haptics.ApplyHapticFeedback(HapticIntensity.Success);
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

        // ── Helpers ────────────────────────────────────────────────────────────

        private IEnumerator WobbleWhenLanded(Crate crate)
        {
            for (float t = 0f; crate != null && crate.Carryable.IsMoving && t < landingTimeout; t += Time.deltaTime)
                yield return null;
            if (crate == null || crate.Carryable.IsMoving || crate.State != CrateState.Slotted) yield break;

            var wobble = crate.GetComponent<PropShake>();
            if (wobble == null) yield break;
            wobble.Shake(bob != null ? crate.transform.position - bob.transform.position : Vector3.zero);
        }

        private void Popup(string message, Color color, Vector3 at)
        {
            if (particles == null) return;
            GameObject go = particles.Get(PopupPoolId);
            if (go == null) return;
            var popup = go.GetComponent<ComicPopup>();
            if (popup == null)
            {
                particles.Release(PopupPoolId, go);
                return;
            }
            popup.Play(message, color, at + Vector3.up * popupHeight);
            particles.ReleaseAfterDelay(PopupPoolId, go, popup.Lifetime);
        }
    }
}
