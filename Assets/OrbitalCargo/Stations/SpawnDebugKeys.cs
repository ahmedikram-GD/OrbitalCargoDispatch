using OrbitalCargo.Config;
using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

namespace OrbitalCargo.Stations
{
    /// <summary>
    /// Editor-only test aid: number keys drop an extra crate on the belt without waiting for the
    /// schedule — 1 standard, 2 cryo, 3 junk, 4 volatile (the order of <see cref="crateTypes"/>),
    /// 5 a suspicious crate hiding contraband, 6 a suspicious false alarm.
    /// Compiled out of player builds; <see cref="SpawnNow"/> stays callable for MCP probes.
    /// </summary>
    [AddComponentMenu("OrbitalCargo/Stations/Spawn Debug Keys (Editor)")]
    public sealed class SpawnDebugKeys : MonoBehaviour
    {
        [SerializeField] private CrateSpawner spawner;
        [Tooltip("Index 0 = key 1, index 1 = key 2, ...")]
        [SerializeField] private CrateTypeSO[] crateTypes;
        [Tooltip("Hidden item for key 5 (a bad suspicious crate). Key 6 drops a clean (false-alarm) one.")]
        [SerializeField] private HiddenItemSO suspiciousItem;

#if UNITY_EDITOR
        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || crateTypes == null) return;
            if (kb.digit1Key.wasPressedThisFrame) SpawnNow(0);
            if (kb.digit2Key.wasPressedThisFrame) SpawnNow(1);
            if (kb.digit3Key.wasPressedThisFrame) SpawnNow(2);
            if (kb.digit4Key.wasPressedThisFrame) SpawnNow(3);
            if (kb.digit5Key.wasPressedThisFrame) SpawnSuspicious(true);
            if (kb.digit6Key.wasPressedThisFrame) SpawnSuspicious(false);
        }
#endif

        /// <summary>A suspicious standard crate: <paramref name="bad"/> hides the item, otherwise it's a false alarm.</summary>
        public void SpawnSuspicious(bool bad)
        {
            if (spawner == null || crateTypes == null || crateTypes.Length == 0) return;
            spawner.Enqueue(new SpawnRequest { Type = crateTypes[0], Suspicious = true, Hidden = bad ? suspiciousItem : null });
            Debug.Log($"[SpawnDebugKeys] Queued a suspicious crate ({(bad ? "bad" : "clean")}).", this);
        }

        public void SpawnNow(int index)
        {
            if (spawner == null || crateTypes == null || index < 0 || index >= crateTypes.Length || crateTypes[index] == null) return;
            spawner.Enqueue(new SpawnRequest { Type = crateTypes[index] });
            Debug.Log($"[SpawnDebugKeys] Queued {crateTypes[index].name} for the belt.", this);
        }
    }
}
