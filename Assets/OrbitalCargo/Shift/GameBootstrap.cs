using GameKit.Architecture.PoolSystem;
using THEBADDEST.SoundSystem;
using UnityEngine;

namespace OrbitalCargo.Shift
{
    /// <summary>
    /// Gives the kit's ScriptableObject services their scene lifecycle, first thing in the scene: starts
    /// the <see cref="SoundService"/> and builds every <see cref="PoolManagerSO"/> under one root. The
    /// kit's own AppBase does this too but drags in remote config and analytics, so this is the lean
    /// equivalent. Slice 6 extends it to boot the FSM.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [AddComponentMenu("OrbitalCargo/Shift/Game Bootstrap")]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private SoundService soundService;
        [Tooltip("CratePooler, ParticlesPooler, UIPooler...")]
        [SerializeField] private PoolManagerSO[] pools;

        private void Awake()
        {
            if (soundService != null) soundService.Initialize();
            else Debug.LogWarning("[GameBootstrap] No SoundService assigned — the game will be silent.", this);

            var poolRoot = new GameObject("PoolRoot").transform;
            foreach (var pool in pools)
                if (pool != null) pool.Initialize(poolRoot);
        }
    }
}
