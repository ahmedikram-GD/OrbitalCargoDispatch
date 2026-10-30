using DG.Tweening;
using OrbitalCargo.Config;
using OrbitalCargo.Crates;
using OrbitalCargo.Interaction;
using UnityEngine;

namespace OrbitalCargo.Stations
{
    /// <summary>
    /// Discovery hook, step 2 — "Scan it" (GDD, "the reveal is the star"). Put a crate in the booth and,
    /// over <see cref="ShiftConfigSO.ScanDurationSeconds"/>, its casing turns to see-through X-ray, the
    /// hidden item (if any) shows inside, and a glowing scan line sweeps down. The status light ends
    /// red (contraband → chute it) or green (false alarm → ship it). A bad crate keeps its X-ray look
    /// so the player can see what they're carrying to the chute. The crate is then pickable from the booth.
    /// Feedback (sound, haptics) comes from the juice presenter via e_ScanStarted / e_ScanResult.
    /// </summary>
    [AddComponentMenu("OrbitalCargo/Stations/Scanner Booth")]
    public sealed class ScannerBooth : MonoBehaviour, IInteractable
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("Where the scanned crate sits (ScannerBooth/CratePoint).")]
        [SerializeField] private Transform cratePoint;
        [Tooltip("Glowing plane that sweeps top → bottom during a scan (ScannerBooth/ScanLine).")]
        [SerializeField] private Transform scanLine;
        [Tooltip("Lamp on the booth roof (ScannerBooth/StatusLight); material needs emission.")]
        [SerializeField] private Renderer statusLight;
        [Tooltip("See-through casing material used during the reveal (M_XRayCasing).")]
        [SerializeField] private Material xrayMaterial;
        [SerializeField] private ShiftConfigSO config;
        [Tooltip("FP_InteractionConfig — prompt text and snap timing.")]
        [SerializeField] private InteractionConfigSO interaction;

        [Header("Events")]
        [SerializeField] private GameEventWithCrate onScanStarted;
        [Tooltip("e_ScanResult. Raised when the reveal finishes; crate.IsBad tells red from green.")]
        [SerializeField] private GameEventWithCrate onScanResult;

        [Header("Look")]
        [Tooltip("Scan line height (m, booth-local) at the start and end of the sweep.")]
        [SerializeField] private float sweepTop = 1.5f;
        [SerializeField] private float sweepBottom = 0.6f;
        [ColorUsage(false, true)] [SerializeField] private Color idleLight = new Color(0.2f, 0.6f, 1f) * 0.6f;
        [ColorUsage(false, true)] [SerializeField] private Color scanningLight = new Color(1f, 0.7f, 0.1f) * 2f;
        [ColorUsage(false, true)] [SerializeField] private Color badLight = new Color(1f, 0.1f, 0.05f) * 3f;
        [ColorUsage(false, true)] [SerializeField] private Color cleanLight = new Color(0.2f, 1f, 0.3f) * 3f;

        private MaterialPropertyBlock _block;
        private Renderer _scanLineRenderer;
        private Sequence _scan;

        /// <summary>The crate in the booth (scanning or waiting to be picked up), or null.</summary>
        public Crate Occupant { get; private set; }
        public bool IsScanning { get; private set; }

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (scanLine != null)
            {
                _scanLineRenderer = scanLine.GetComponent<Renderer>();
                if (_scanLineRenderer != null) _scanLineRenderer.enabled = false;
            }
            SetLight(idleLight);
        }

        // ── IInteractable ──────────────────────────────────────────────────────

        public string Prompt(CrateCarrier carrier) =>
            carrier != null && carrier.HasCrate && Occupant == null && interaction != null ? interaction.ScanPrompt : string.Empty;

        public bool CanInteract(CrateCarrier carrier) => carrier != null && carrier.HasCrate && Occupant == null;

        public void Interact(CrateCarrier carrier, RaycastHit hit)
        {
            if (!CanInteract(carrier)) return;
            BeginScan(carrier.PopTop());
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>Take <paramref name="crate"/> into the booth and run the reveal. False if the booth is occupied.</summary>
        public bool BeginScan(Crate crate)
        {
            if (crate == null || Occupant != null || config == null) return false;

            Occupant = crate;
            IsScanning = true;
            crate.StateChanged += HandleOccupantStateChanged;
            crate.SetState(CrateState.Scanning);
            crate.Carryable.SnapTo(cratePoint, interaction != null ? interaction.SnapDuration : 0.15f,
                                   interaction != null ? interaction.FlyArcHeight : 0f);
            if (onScanStarted != null) onScanStarted.Raise(crate);

            float duration = config.ScanDurationSeconds;
            var visuals = crate.GetComponent<CrateVisuals>();
            SetLight(scanningLight);

            _scan?.Kill();
            _scan = DOTween.Sequence().SetLink(gameObject);
            // 15% in: the casing goes see-through and whatever's inside appears.
            _scan.InsertCallback(duration * 0.15f, () =>
            {
                if (visuals == null) return;
                visuals.SetXRay(true, xrayMaterial);
                visuals.ShowHidden(crate.HiddenItem);
            });
            if (scanLine != null)
            {
                _scan.InsertCallback(0f, () =>
                {
                    scanLine.localPosition = new Vector3(scanLine.localPosition.x, sweepTop, scanLine.localPosition.z);
                    if (_scanLineRenderer != null) _scanLineRenderer.enabled = true;
                });
                _scan.Insert(0f, scanLine.DOLocalMoveY(sweepBottom, duration).SetEase(Ease.InOutSine));
            }
            _scan.AppendCallback(() => FinishScan(crate, visuals));
            return true;
        }

        /// <summary>
        /// Shift end: abort any reveal and hand back the crate in the booth (null if empty) without a
        /// result. The caller disposes of it; the booth is idle again.
        /// </summary>
        public Crate Evict()
        {
            _scan?.Kill();
            _scan = null;
            IsScanning = false;
            if (_scanLineRenderer != null) _scanLineRenderer.enabled = false;

            var crate = Occupant;
            if (crate != null) crate.StateChanged -= HandleOccupantStateChanged;
            Occupant = null;
            SetLight(idleLight);
            return crate;
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void FinishScan(Crate crate, CrateVisuals visuals)
        {
            IsScanning = false;
            if (_scanLineRenderer != null) _scanLineRenderer.enabled = false;
            if (crate == null) return;

            crate.IsScanned = true;
            // A clean crate goes back to normal; a bad one stays X-rayed so the egg is visible on the way to the chute.
            if (!crate.IsBad && visuals != null) visuals.SetXRay(false, null);
            SetLight(crate.IsBad ? badLight : cleanLight);
            crate.Carryable.CompleteMove();
            crate.SetState(CrateState.Pickable);
            if (onScanResult != null) onScanResult.Raise(crate);
        }

        private void HandleOccupantStateChanged(Crate crate, CrateState state)
        {
            if (state == CrateState.Scanning || state == CrateState.Pickable) return;
            // Picked up (or otherwise gone): the booth is free again.
            crate.StateChanged -= HandleOccupantStateChanged;
            if (crate != Occupant) return;
            Occupant = null;
            SetLight(idleLight);
        }

        private void SetLight(Color emission)
        {
            if (statusLight == null) return;
            _block ??= new MaterialPropertyBlock();
            statusLight.GetPropertyBlock(_block);
            _block.SetColor(EmissionColorId, emission);
            _block.SetColor(BaseColorId, emission.maxColorComponent > 0f ? emission / emission.maxColorComponent : emission);
            statusLight.SetPropertyBlock(_block);
        }
    }
}
