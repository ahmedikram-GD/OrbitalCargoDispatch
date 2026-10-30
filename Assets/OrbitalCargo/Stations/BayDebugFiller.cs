using GameKit.Architecture.PoolSystem;
using OrbitalCargo.Config;
using OrbitalCargo.Crates;
using OrbitalCargo.Interaction;
using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

namespace OrbitalCargo.Stations
{
    /// <summary>
    /// Editor-only test aid: press <b>F</b> while aiming at a bay (its pad, a crate in it, or its launch
    /// pedestal) to fill every empty slot with pooled crates of that bay's own type, so a launch can be
    /// tested without hauling six crates. The key is compiled out of player builds;
    /// <see cref="FillFocusedBay"/> stays callable for MCP probes.
    /// </summary>
    [AddComponentMenu("OrbitalCargo/Stations/Bay Debug Filler (Editor)")]
    public sealed class BayDebugFiller : MonoBehaviour
    {
        private const string CratePoolId = "Crate";

        [Tooltip("The player's CrateInteractor (scene, set by SceneSetup) — tells us which bay is aimed at.")]
        [SerializeField] private CrateInteractor interactor;
        [Tooltip("CratePooler — filler crates come from and return to the same pool as real ones.")]
        [SerializeField] private PoolManagerSO cratePooler;
        [Tooltip("Crate types to choose from; the one whose Destination matches the bay is used.")]
        [SerializeField] private CrateTypeSO[] crateTypes;

#if UNITY_EDITOR
        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.fKey.wasPressedThisFrame) FillFocusedBay();
        }
#endif

        /// <summary>Fill the aimed-at bay. Returns how many crates were added (0 if no bay is aimed at).</summary>
        public int FillFocusedBay()
        {
            Bay bay = ResolveBay(interactor != null ? interactor.FocusedTarget : null);
            if (bay == null)
            {
                Debug.Log("[BayDebugFiller] Aim at a bay (pad, crate or launch pedestal) and press F.", this);
                return 0;
            }
            if (bay.IsLocked || cratePooler == null) return 0;

            CrateTypeSO type = TypeFor(bay.Id);
            int added = 0;
            while (!bay.Model.IsFull)
            {
                GameObject go = cratePooler.Get(CratePoolId);
                if (go == null) break;
                var crate = go.GetComponent<Crate>();
                crate.Configure(type, false, null);
                if (!bay.TryPlace(crate, bay.transform.position))
                {
                    cratePooler.Release(CratePoolId, go);
                    break;
                }
                added++;
            }
            Debug.Log($"[BayDebugFiller] Filled {bay.name} with {added} × {(type != null ? type.name : "crate")}.", this);
            return added;
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static Bay ResolveBay(IInteractable target)
        {
            if (target is Bay bay) return bay;
            if (target is LaunchButton button && button.Launcher != null) return button.Launcher.Bay;
            return null;
        }

        private CrateTypeSO TypeFor(BayId id)
        {
            if (crateTypes == null || crateTypes.Length == 0) return null;
            foreach (var t in crateTypes)
                if (t != null && t.Destination == id) return t;
            return crateTypes[0];
        }
    }
}
