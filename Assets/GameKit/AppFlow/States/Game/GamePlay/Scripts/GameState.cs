using UnityEngine;
using GameKit.UI;
using System.Collections;
using GameKit.StateMachine;
using Sirenix.OdinInspector;
using GameKit.StateMachine.Application;
using GameKit.Architecture.PoolSystem;
using GameKit.Architecture.Events;
using GameKit.AppFlow;
using GameKit.Architecture;

namespace GameKit.AppFlow
{

public abstract class GameState : State
{
    [SerializeField] private string gameplayPrefabId;
    [SerializeField] private string hudPrefabId;
    
    [SerializeField] private bool poolGameplay = true;
    [SerializeField] private bool poolGameHud = true;
    
    [ShowIf("@poolGameplay || poolGameHud")]
    [SerializeField, Required]
    [InfoBox("Ensure prefab is registered in PoolManagerSO.", InfoMessageType.None)]
    protected PoolManagerSO statePooler;


    [Header("State Events")]
    [SerializeField] private GameEvent GameStateEnter;
    [SerializeField] private GameEvent GameStatePaused;
    [SerializeField] private GameEvent GameStateResumed;
    [SerializeField] private GameEvent GameStateExit;

    protected GameHud gameHudInstance;
    private IShowable _iShowable;
    protected GameObject gameplayInstance;
    protected GameObject _spawnedHud;

    public override IEnumerator Enter(IState previous)
    {
        yield return base.Enter(previous);

        // Defensive single-instance guard. A previous Exit() may have been interrupted — a transition
        // coroutine stopped mid-`gameHudInstance.Hide()` by an overlapping transition (the flow controller
        // even warns about this) leaves the HUD + gameplay alive because the release runs AFTER Hide().
        // Re-entering would then spawn a second HUD + gameplay → the "two gameplays" duplication. Releasing
        // any leftovers here (synchronously, so it can't itself be interrupted) makes the state strictly
        // single-instance: it can never own more than one HUD + gameplay.
        ReleaseSpawnedInstances();

        //Debug.Log("Entering game state!");
        // 1. Load Gameplay
        if (!string.IsNullOrEmpty(gameplayPrefabId))
        {
            if (poolGameplay)
            {
                gameplayInstance = statePooler.Get(gameplayPrefabId);
            }
            else
            {
                var gameplayPrefab = Resources.Load<GameObject>(gameplayPrefabId);
                if (gameplayPrefab != null)
                {
                    gameplayInstance = Instantiate(gameplayPrefab,StateRootManager.States);
                }
                else
                {
                    Debug.LogWarning($"Gameplay prefab '{gameplayPrefabId}' not found in Resources.");
                }
            }

            if (gameplayInstance != null)
                gameplayInstance.transform.SetParent(StateRootManager.States, false);
        }

        // 2. Load HUD
        if (poolGameHud)
        {
            _spawnedHud = statePooler.Get(hudPrefabId);
            gameHudInstance = _spawnedHud.GetComponent<GameHud>();
            _spawnedHud.transform.SetParent(StateRootManager.UIStates, false);
        }
        else
        {
            var hudPrefab = Resources.Load<GameObject>(hudPrefabId);
            if (hudPrefab == null)
            {
                //Debug.LogError($"[GameState] HUD prefab '{hudPrefabId}' not found in Resources.");
                yield break;
            }
            
            _spawnedHud = Instantiate(hudPrefab, StateRootManager.States);
            gameHudInstance = _spawnedHud.GetComponent<GameHud>();
            _spawnedHud.transform.SetParent(StateRootManager.UIStates, false);
        }

        if (gameHudInstance != null)
        {
            _iShowable = gameHudInstance;
            gameHudInstance.Show();
        }
        else
        {
            Debug.LogError($"[GameState] HUD instance is missing or missing GameHud component.");
        }

        GameStateEnter?.Invoke();
    }

    public override IEnumerator Exit()
    {
        // 1. Hide HUD (WAIT for the hide animation)
        if (gameHudInstance != null)
            yield return gameHudInstance.Hide();

        // 2. Release HUD + gameplay. Synchronous (no yields) so a stopped transition coroutine
        //    can't interrupt it half-done — the only interruptible point is the Hide() above, and
        //    Enter()'s guard cleans up if we never reach here.
        ReleaseSpawnedInstances();

        yield return base.Exit();
        GameStateExit?.Invoke();
    }

    // Instant teardown for bulk clears (e.g. going Home). Releases the HUD + gameplay immediately
    // instead of awaiting the HUD hide animation — otherwise the board/tray keep rendering on a black
    // screen for the whole hide while the destination (Home) builds. No yields → atomic.
    public override IEnumerator ExitImmediate()
    {
        ReleaseSpawnedInstances();
        yield return base.Exit();
        GameStateExit?.Invoke();
    }

    // Releases/destroys the spawned HUD + gameplay and clears the references. Safe to call when nothing
    // is spawned (no-op). Kept free of yields so it executes atomically w.r.t. coroutine cancellation.
    private void ReleaseSpawnedInstances()
    {
        if (_spawnedHud != null)
        {
            if (poolGameHud)
                statePooler.Release(hudPrefabId, _spawnedHud);
            else
                Destroy(_spawnedHud);
        }
        _spawnedHud = null;
        gameHudInstance = null;

        if (gameplayInstance != null)
        {
            if (poolGameplay)
                statePooler.Release(gameplayPrefabId, gameplayInstance);
            else
                Destroy(gameplayInstance);
            gameplayInstance = null;
        }
    }


    public override IEnumerator Pause()
    {
        yield return base.Pause();
        _iShowable?.Pause();
        GameStatePaused?.Invoke();
    }

    public override IEnumerator Resume()
    {
        yield return base.Resume();
        _iShowable?.Resume();
        GameStateResumed?.Invoke();
    }
}

}