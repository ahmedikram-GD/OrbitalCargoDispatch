using System.Collections;
using GameKit.Architecture.PoolSystem;
using GameKit.StateMachine;
using GameKit.StateMachine.Application;
using OrbitalCargo.Flow;
using Sirenix.OdinInspector;
using THEBADDEST.SoundSystem;
using UnityEngine;

namespace OrbitalCargo.Shift
{
    /// <summary>
    /// Gives the kit's ScriptableObject services their scene lifecycle, first thing in the scene: starts
    /// the <see cref="SoundService"/>, builds every <see cref="PoolManagerSO"/> under one root, points the
    /// kit <see cref="StateRootManager"/> at the scene's landscape UI root, then boots the FSM, whose boot
    /// state is the shift itself. The kit's own AppBase does this too but drags in remote config and
    /// analytics, so this is the lean equivalent.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [AddComponentMenu("OrbitalCargo/Shift/Game Bootstrap")]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private SoundService soundService;
        [Tooltip("CratePooler, ParticlesPooler, UIPooler, UIStatesPooler.")]
        [SerializeField] private PoolManagerSO[] pools;

        [Title("Flow")]
        [Required, Tooltip("FSM_Main — boot state ShiftGameState.")]
        [SerializeField] private FiniteStateMachine fsm;
        [Required, SerializeField] private GameFlow gameFlow;
        [Required, Tooltip("Scene 'States' transform (gameplay prefabs; empty here — gameplay is in the scene).")]
        [SerializeField] private Transform statesRoot;
        [Required, Tooltip("Scene 'UIStates' overlay canvas (1920×1080) that FSM views are parented under.")]
        [SerializeField] private Transform uiStatesRoot;

        private void Awake()
        {
            // A play session stopped while paused must not start frozen.
            Time.timeScale = 1f;

            if (soundService != null) soundService.Initialize();
            else Debug.LogWarning("[GameBootstrap] No SoundService assigned — the game will be silent.", this);

            var poolRoot = new GameObject("PoolRoot").transform;
            foreach (var pool in pools)
                if (pool != null) pool.Initialize(poolRoot);

            StateRootManager.Initialize(statesRoot, uiStatesRoot);
        }

        private IEnumerator Start()
        {
            if (fsm == null || gameFlow == null)
            {
                Debug.LogError("[GameBootstrap] FSM or GameFlow missing — run Tools/OrbitalCargo/Setup Game Scene.", this);
                yield break;
            }

            gameFlow.MarkShiftRunning();
            yield return fsm.Init();
        }
    }
}
