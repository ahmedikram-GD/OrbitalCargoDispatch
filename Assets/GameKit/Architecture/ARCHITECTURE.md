# GameKit Architecture — Foundation Layer

The shared, game-agnostic primitives every other GameKit module is built on: ScriptableObject variables,
ScriptableObject events, persistence, remote config, ads, pooling, sound, time and coroutines.

`Assets/GameKit/Architecture/` · 93 C# scripts
Namespaces: `Blues.Core.Variables` · `Blues.Core.Events` · `Blues.Core.PoolSystem` · `Blues.Core.Game` ·
`ProjectCore.PoolSystem` · `ProjectCore.Utilities.Json` · `ProjectCore.Input.SwipeSystem` ·
`ProjectCore.TheTimeMachine` · `THEBADDEST.Coroutines` · `THEBADDEST.SoundSystem` · plus a large set of
**global-namespace** types (`DBInt`, `DBString`, `DBBool`, `DBFloat`, `MonetizationAdsService`,
`FirebaseRemoteFetcherSO`, `IdleTimer`, `ServerTimeService`, `UIValueAnimator`, `MeshCombiner`).

The [LiveOps layer](../LiveOps/ARCHITECTURE.md) sits directly on top of everything here — it stores its save
blobs in `DBString`s, listens on `GameEvent`s, and receives its tuning from `FirebaseRemoteFetcherSO`.

---

## Mental model

**Everything shared is an asset, not a singleton.** State lives in ScriptableObject *variables*
(`Int`, `Bool`, `DBInt`, …), communication happens through ScriptableObject *events* (`GameEvent`,
`GameEventWithParam<T>`), and services (`SoundService`, `PoolManagerSO`, `MonetizationAdsService`,
`FirebaseRemoteFetcherSO`) are themselves ScriptableObjects wired by inspector reference. Two systems talk by
being handed the same asset, never by `FindObjectOfType` or a static instance.

The cost of that model is Unity's SO lifetime: an SO's fields survive scene loads *and* editor play sessions,
while `OnEnable`/`OnDisable` fire at asset load/unload — not at scene boundaries. Most of the sharp edges in
this layer (stale event subscriptions, in-memory values outliving a prefs wipe, asset files drifting in git)
trace back to that single fact.

---

## Sub-systems

### Variables — `Variables/`

Two families over one shape: a `Value` field, a `DefaultValue` field, and `GetValue()` / `SetValue()`.
Non-persistent types live in `Blues.Core.Variables`; the `DB*` persistent types are in the **global**
namespace and subclass their non-persistent counterpart, adding a PlayerPrefs `key`.

| Type | File | Role |
|---|---|---|
| `Int` · `Float` · `Double` · `Bool` · `SharedString` | [Non-Persistent/](Variables/Non-Persistent/) | Base SO variables. `GetValue`/`SetValue`, plus `Increment`/`Decrement` on the numeric ones. `ResetToDefaultOnPlay` resets in `OnEnable`. |
| `SharedVector2` · `SharedVector3` · `SharedVector2Int` · `SharedVector3Int` | [Non-Persistent/](Variables/Non-Persistent/) | Same shape for vector payloads. |
| `IntWithEvent` · `BoolWithEvent` · `StringWithEvent` | [Non-Persistent/IntWithEvent.cs](Variables/Non-Persistent/IntWithEvent.cs) | Subclass that fires a serialized `GameEvent` after every mutation. |
| `SharedDictionary<K,V>` · `SharedDictionaryIntString` | [Non-Persistent/SharedDictionary.cs](Variables/Non-Persistent/SharedDictionary.cs) | Odin-serialized SO dictionary. **`OnEnable` allocates a fresh empty dictionary** — it is a runtime scratchpad, never authored data. |
| `DBInt` · `DBFloat` · `DBBool` · `DBString` | [Persistent/DBInt.cs](Variables/Persistent/DBInt.cs), [DBString.cs](Variables/Persistent/DBString.cs) | PlayerPrefs-backed. `Load()` on `OnEnable`, `Save()` writes `PlayerPrefs.Set*(key, Value)` + `PlayerPrefs.Save()`. |
| `DBIntWithEvent` · `DBFloatWithEvent` · `DBBoolWithEvent` · `DBStringWithEvent` | [Persistent/](Variables/Persistent/) | Persistent + `GameEvent` fan-out. Note these call `GameEvent.Invoke()` **unguarded** (no `?.`) — an unassigned event throws. |
| `KeyRegistry` | [Helper/KeyRegistry.cs](Variables/Helper/KeyRegistry.cs) | **Dead code** — the entire file is commented out. Keys are authored by hand on each asset. |

```csharp
// BaseGameManager.cs:103 / :180 — reading and mutating shared level counters
public void LoadCurrentLevel() => LoadLevel(levelNo != null ? levelNo.GetValue() : 1);
levelNoForText?.Increment(1);          // DBInt.Increment → base.Increment + Save()

// MonetizationAdsService.cs:32 — a DBBool used as a purchase gate
levelNo.GetValue() < adsRemoteSettings.ShowBannerFromLevel || removeAds.GetValue();

// ILiveOpStore.cs:110 — DBString is the ONE type whose SetValue does not persist
_db.SetValue(json);
_db.Save(); // DBString.SetValue does not persist on its own — force the durable write.
```

### Events — `Utilities/Events/`

`namespace Blues.Core.Events`. Parameterless events are `GameEvent`; typed ones derive from the generic
`GameEventWithParam<T>` (1/2/3-arg variants exist; only the 1-arg concretes are used).

| Type | File | Role |
|---|---|---|
| `GameEvent` | [Events/GameEvent.cs](Utilities/Events/GameEvent.cs) | `Subscribe` / `UnSubscribe` / `Invoke` / `HasListeners`. `Invoke` walks `GetInvocationList()` and calls each subscriber inside its own `try/catch` so one thrower can't drop the rest (`GameEvent.cs:19-31`). **Nulls `_handler` in `OnDisable` (`:48`).** |
| `GameEventWithParam<T>` | [Events/GameEventWithParam.cs](Utilities/Events/GameEventWithParam.cs) | Generic base. `Raise(T)` is a plain multicast invoke — *no* per-subscriber isolation, and it does **not** clear handlers in `OnDisable`. |
| `GameEventWithBool` · `GameEventWithInt` · `GameEventWithString` · `GameEventWithVector2Int` | [Events/](Utilities/Events/) | Concrete assets. Each just overrides `Raise` to add an Odin `[Button]`. |
| `GameEventWithReturn<T>` · `GameEventWithReturnInt` | [Events/GameEventWithReturn.cs](Utilities/Events/GameEventWithReturn.cs) | Query-style event over `Func<T>`; `Raise()` returns `default` when unsubscribed. |
| `GameEventButton` | [Events/GameEventButton.cs](Utilities/Events/GameEventButton.cs) | `[RequireComponent(typeof(Button))]` MonoBehaviour that self-binds `onClick → gameEvent.Invoke()` in `Awake`. |

**The subscribe pattern.** Because `GameEvent.OnDisable` sets `_handler = null`, any subscriber that is itself
a ScriptableObject (and therefore only ever subscribes once, at boot) is silently and permanently detached the
first time the event asset unloads. MonoBehaviours are immune — they re-subscribe in `OnEnable`. SOs must use
a **self-healing, idempotent detach-then-attach** entry point that a reliable caller re-asserts:

```csharp
// LiveOpsDispatcher.cs:52 — Initialize() is re-called from HomeState.Enter() on EVERY home entry
public void Initialize()
{
    if (onHomeLanding != null) { onHomeLanding.UnSubscribe(HandleHomeLanding); onHomeLanding.Subscribe(HandleHomeLanding); }
    if (onLevelWin != null)    { onLevelWin.UnSubscribe(HandleLevelWin);       onLevelWin.Subscribe(HandleLevelWin); }
    if (onLevelFail != null)   { onLevelFail.UnSubscribe(HandleLevelFail);     onLevelFail.Subscribe(HandleLevelFail); }
}

// BaseGameManager.cs:77 — MonoBehaviours use the plain OnEnable/OnDisable pair instead
protected virtual void OnEnable()  { loadCurrentLevelEvent?.Subscribe(LoadCurrentLevel); }
protected virtual void OnDisable() { loadCurrentLevelEvent?.UnSubscribe(LoadCurrentLevel); }
```

### AdPackage — `AdPackage/Scripts/`

| Type | File | Role |
|---|---|---|
| `MonetizationAdsService` | [AdPackage/Scripts/MonetizationAdsService.cs](AdPackage/Scripts/MonetizationAdsService.cs) | `ScriptableObject` wrapper over `Monetization.Runtime.Ads.AdsManager`. Owns all show/hide gating for banner, interstitial and rewarded. |
| `IAdsService` | [AdPackage/Scripts/IAdsService.cs](AdPackage/Scripts/IAdsService.cs) | **Unused** — nothing implements it (`MonetizationAdsService` does not), and every call site references the concrete class. |

Gates, all inside the service so states stay dumb:
- **Banner** — `levelNo < ShowBannerFromLevel || removeAds` ⇒ actively `HideBanner()` rather than returning
  (`:38-46`). On no fill, the HUD area stays hidden and a token-cancelled watcher coroutine shows it the moment
  an ad loads (`WaitForBannerFill`, `:66`). `OnBannerVisible(BannerStatus.Show|Hide|Destroy)` drives the HUD.
- **Interstitial** — level gate → `removeAds` gate → **time-based** cooldown
  (`Time.unscaledTime - _lastFullscreenAdClosedAt < InterstitialCooldownSeconds`) → SDK readiness. Every
  rejection still calls `onComplete`, so flows never stall.
- **Rewarded** — no gate; `onDismissed(true|false)` distinguishes earned from dismissed. A rewarded video that
  EARNS its reward calls `ResetInterstitialCooldown` too, so the two ad types share one cooldown clock.

```csharp
// LevelCompleteState.cs:140 / :165
adsService.OnLevelComplete();                     // no-op since the cooldown became time-based
bool interShown = adsService.TryShowInterstitial("LevelComplete", onComplete: null);
adsService.ShowRewarded("LevelFail_OutOfMoves", rewarded => { if (rewarded) Revive(); });
// BannerController.cs:27 — a state's ShowBanner flag is the only banner input
if (state.ShowBanner) _adsService.ShowBanner(); else _adsService.HideBanner();
```

The cooldown is measured in **`Time.unscaledTime`**, which does not advance while an ad is on screen or the
app is backgrounded — so it counts time the player spent PLAYING, not time spent watching the previous ad. It
is session state (`[NonSerialized]`, starting at app launch) and is deliberately not persisted across a
restart. `ExtendInterstitialTime()` now forwards to `ResetInterstitialCooldown()` instead of being a no-op;
`OnLevelComplete()` is the one that became a **no-op**, kept for call-site compatibility.
`ShowInter()` is an editor-only `[Button]`.

### FirebaseRemoteArchitecture — `FirebaseRemoteArchitecture/`

| Type | File | Role |
|---|---|---|
| `FirebaseRemoteFetcherSO` | [Scripts/FirebaseRemoteFetcherSO.cs](FirebaseRemoteArchitecture/Scripts/FirebaseRemoteFetcherSO.cs) | The whole pipeline: registers bundled JSON as defaults, subscribes to the fetch callback, parses and pushes values into config SOs. |
| `BaseLifeOpsData` | [Scripts/BaseLifeOpsData.cs](FirebaseRemoteArchitecture/Scripts/BaseLifeOpsData.cs) | Shared per-LiveOp contract: `eventId`, `unlockLevel`, `timeToExpire`, `popupThreshold`, `onboardingSortOrder`, `expireSortOrder`, `sortOrder`. |
| `IRemoteLiveOpsFetcher` | [Scripts/IRemoteLiveOpsFetcher.cs](FirebaseRemoteArchitecture/Scripts/IRemoteLiveOpsFetcher.cs) | `string EventId { get; }` + `ApplyLiveOpsData(BaseLifeOpsData)`. Legacy seam — the fetcher no longer calls through it (see gotchas). |
| Data contracts | same file, `:715-862` | `GameConfigData`, `AdsConfigData`, `BoostersConfigWrapper`, `AbTestConfigData`, `LevelDataWrapper`. `LiveOpsEntry` also survives here — not because anything feeds it a remote key any more, but because every `LiveOpBaseConfig` subclass elsewhere in `GameKit.Runtime` still takes one in `ApplyLiveOpsData(LiveOpsEntry)`. `LiveOpsConfigData`, `FeatureUnlockData` and `TrayConfigData` are **deleted** — Numberix ships no live-ops config, and feature-unlock/themes are parsed and applied game-side instead (see the FirebaseRemoteArchitecture table above). Field names must match JSON keys exactly (`JsonUtility`). |
| Default JSONs | [Json/](FirebaseRemoteArchitecture/Json/) | `GameConfig.json`, `AdsConfig.json`, `BoostersConfig.json`, `FeatureUnlockConfig.json`, `ThemesConfig.json`, `AbTestConfig.json`, `Leveldata.json.txt`. `LiveOpsConfig.json`, `TrayConfig.json` and `Json/Old/` are **deleted** — there is no live-ops or tray-expansion remote config in this project any more. |

### Coroutines — `Utilities/CoroutineHandler-master/`

Third-party package (`THEBADDEST.Coroutines`, own asmdef `Coroutines`) letting non-MonoBehaviours run
coroutines off a hidden `DontDestroyOnLoad` `MonoRunner`.

| Type | File | Role |
|---|---|---|
| `CoroutineHandler` | [Runtime/CoroutineHandler.cs](Utilities/CoroutineHandler-master/Runtime/CoroutineHandler.cs) | `StartStaticCoroutine` / `StopStaticCoroutine` / `StopAll`, `AfterWait` (delay, sequence, or predicate), `WaitLoop`, `DoUpdate`. |
| `MonoRunner` | [Runtime/MonoRunner.cs](Utilities/CoroutineHandler-master/Runtime/MonoRunner.cs) | Empty host MonoBehaviour, `HideFlags.HideInHierarchy`. |
| `CoroutineSequence` · `CoroutineDelay` · `CoroutineCondition` · `CoroutineLoop` · `CoroutineBuilder` · `TimeCounter` | [Runtime/](Utilities/CoroutineHandler-master/Runtime/) | Composable yield primitives. |
| `THEBADDEST.Coroutines.TimeMachine` | [Runtime/TimeMachine.cs](Utilities/CoroutineHandler-master/Runtime/TimeMachine.cs) | Package's own second-tick SO — **name-collides with the project's `ProjectCore.TheTimeMachine.TimeMachine`**; the project uses the latter. |

`StartStaticCoroutine` guards against a destroyed runner (`EnsureRunner`, `:61`): with editor domain reload
disabled the static `mono` can be Unity-fake-null early in boot, which previously stranded callers such as the
LiveOps home-landing flow with the home lock never released.

```csharp
// MonetizationAdsService.cs:63 — run a coroutine from a ScriptableObject
CoroutineHandler.StartStaticCoroutine(WaitForBannerFill(token));
```

### Pool system — `Utilities/PoolSystem/`

| Type | File | Role |
|---|---|---|
| `PoolManagerSO` | [Scripts/PoolManagerSO.cs](Utilities/PoolSystem/Scripts/PoolManagerSO.cs) | Asset holding `PoolConfig[]` (`PoolID`, `Prefab`, `DefaultCapacity`, `MaxSize`, `Prewarm`). `Initialize` / `Get` / `Release` / `ReleaseAfterDelay`. |
| `UnityObjectPool<T>` | [Scripts/UnityObjectPool.cs](Utilities/PoolSystem/Scripts/UnityObjectPool.cs) | Thin wrapper over `UnityEngine.Pool.ObjectPool<T>`; auto-activates/deactivates GameObjects & Components, swallows double-release with a warning. |
| `IPool<T>` · `IPoolable` | [Scripts/IPool.cs](Utilities/PoolSystem/Scripts/IPool.cs), `PoolManagerSO.cs:159` | Pool contract; `IPoolable.OnPoolGet/OnPoolRelease` callbacks on pooled prefabs. |
| `PooledFxAutoRelease` | [Scripts/PooledFxAutoRelease.cs](Utilities/PoolSystem/Scripts/PooledFxAutoRelease.cs) | `Arm(pool, poolId, delay)` self-returns a particle FX and stops emission on `OnDisable`. |
| `PoolManagerRunner` | [Scripts/PoolManagerRunner.cs](Utilities/PoolSystem/Scripts/PoolManagerRunner.cs) | Empty static coroutine host for delayed releases. |
| `PoolMonitor` | [Scripts/PoolMonitor.cs](Utilities/PoolSystem/Scripts/PoolMonitor.cs) | **Stubbed** — its whole body is commented out (`DebugAllPools()` no longer exists). |

```csharp
// ChainItemHandler.cs:190 — the standard get/auto-release pair
GameObject particles = poolManager.Get(tickParticlesID);   // returns null (logged) if the id is missing
poolManager.ReleaseAfterDelay(tickParticlesID, particles, 2f);
```

`Initialize` is guarded on `_poolRoot != null && _gameObjectPools.Count > 0` (`:54`) — both conditions matter:
guarding on the root alone let a stale persisted root skip `CreatePools()` and made later `Get()` calls fail.

### Sound — `Utilities/SoundSystem/`

| Type | File | Role |
|---|---|---|
| `SoundService` | [Scripts/SoundService.cs](Utilities/SoundSystem/Scripts/SoundService.cs) | `ScriptableObject, IList<Sound>`. `Initialize` / `Shutdown` / `Play(name[, delay\|position])` / `Stop` / `StopAllSounds` / `FadeIn` / `FadeOut` / `SetVolume` / `SetMute` / `Prewarm`. |
| `AudioSourcePool` | [Scripts/AudioSourcePool.cs](Utilities/SoundSystem/Scripts/AudioSourcePool.cs) | `ObjectPool<AudioSource>` over a `DontDestroyOnLoad` "GameSounds" root; resets every source on take. |
| `Sound` · `SoundType` | [Scripts/Sound.cs](Utilities/SoundSystem/Scripts/Sound.cs) | Serializable entry: clip(s), loop, random clip/pitch, volume, `SFX \| UI \| Music`. |
| `SoundSettings` | [Scripts/SoundSettings.cs](Utilities/SoundSystem/Scripts/SoundSettings.cs) | `AudioMixer` volumes per channel; `SoundExtensions` loads it from `Resources/SoundSettings`. |

`Initialize()` explicitly clears `activeFades` / `activeLoopSources` / `playingLoopSounds` and nulls every
`Sound.Source` (`:39-51`) — those instance collections survive domain reloads and would otherwise point at
GameObjects destroyed with the previous play session.

```csharp
// AppBase.cs:40 + any view
soundService.Initialize();
soundService.Play("Click");
```

### Time — `Utilities/TimeSystem/`, `Utilities/IdleTimer/`

| Type | File | Role |
|---|---|---|
| `ServerTimeService` | [TimeSystem/Scripts/ServerTimeService.cs](Utilities/TimeSystem/Scripts/ServerTimeService.cs) | `static IEnumerator FetchServerTime(onSuccess, onFailure)` — HTTP `HEAD https://www.google.com`, 3 s timeout, parses the `Date` header to UTC. Re-entrancy-guarded; fires `NoInternetAction` on failure. |
| `TimeMachine` | [TimeSystem/Scripts/TimeMachine.cs](Utilities/TimeSystem/Scripts/TimeMachine.cs) | `ProjectCore.TheTimeMachine`. `Tick()` coroutine invoking a `GameEvent` (`e_Tick`) once per second; self-guards against a double start. |
| `IdleTimer` | [IdleTimer/Scripts/IdleTimer.cs](Utilities/IdleTimer/Scripts/IdleTimer.cs) | SO that counts seconds off `e_Tick` while `controlsLocked` is false and fires `triggerAction` at `triggerPoint`. `Init()` / `Cleanup()` / `ResetTimer()`, guarded by an `initialized` flag. |
| `GoogleDateFetcher` | [TimeSystem/Scripts/GoogleDateFetcher.cs](Utilities/TimeSystem/Scripts/GoogleDateFetcher.cs) | Editor `[Button]` debug helper; not used at runtime. |

Countdown/expiry windows are **not** here — `TimeExpiryServiceSO` lives under
[`Assets/GameKit/LiveOps/TimeExpiry/`](../LiveOps/TimeExpiry/) and consumes `ServerTimeService`; see the
[LiveOps architecture](../LiveOps/ARCHITECTURE.md#3-abstract-base--infrastructure).

### Input, JSON and misc utilities

| Type | File | Role |
|---|---|---|
| `SwipeDetector` · `KeyboardSwipeSimulator` · `ISwipeDetector` · `SwipeData` · `SwipeDirection` · `InputController` | [Utilities/SwipeSystem/Scripts/](Utilities/SwipeSystem/Scripts/) | Touch/keyboard swipe detection merged into a single `static Action<SwipeData> InputController.SwipeAction`. |
| `JsonFileReader` · `JsonFileWriter` · `IJsonReader` · `IJsonWriter` | [Utilities/Json/Scripts/](Utilities/Json/Scripts/) | Newtonsoft read/write to a filepath. |
| `JsonEncryptor` · `JsonValidator` · `JsonPrettyPrinter` · `StudentsJsonHandler` | [Utilities/Json/Scripts/](Utilities/Json/Scripts/) | **Sample/demo** tier — `StudentsJsonHandler` drives the `JsonTestingScene`; `JsonEncryptor` ships a placeholder key (`"your-encryption-key"`) and is not referenced by game code. |
| `MeshCombiner` | [Utilities/MeshCombiningUtility/Scripts/MeshCombiner.cs](Utilities/MeshCombiningUtility/Scripts/MeshCombiner.cs) | `static Mesh CombineMeshesFromRoot(Transform, bool)` + UV refit. The `disableOriginalRenderers` branch is commented out — the parameter is currently inert. |
| `UIValueAnimator` | [Utilities/UIValueAnimator.cs](Utilities/UIValueAnimator.cs) | `AnimateIntValue(runner, tmpText, from, to, duration)` counting-number tween. |
| `ColorFoldoutGroupAttribute` | [Utilities/CustomEditorTools/Scripts/ColorFoldoutGroupAttribute.cs](Utilities/CustomEditorTools/Scripts/ColorFoldoutGroupAttribute.cs) | Odin `FoldoutGroup` with a tinted header; drawer is `#if UNITY_EDITOR`. |

### Game management & state glue

| Type | File | Role |
|---|---|---|
| `BaseGameManager` | [GameManagement/BaseGameManager.cs](GameManagement/BaseGameManager.cs) | Abstract MonoBehaviour owning the meta loop shared by every game on the template: level counters, level resolution via `LevelsData`, win/fail sequences, booster unlock-at-level, continue/retry. Concrete games override `OnLoadLevel` / `OnWin` / `OnFail` / `OnContinue`. |
| `GameStateTransition` | [GameStateTransition.cs](GameStateTransition.cs) | Empty `Transition` subclass — exists only to give the state machine a `[CreateAssetMenu]` entry. |
| `NormalGameState` | [NormalGameState.cs](NormalGameState.cs) | `GameState` that exposes `GoToLevelCompleteState()` / `GoToLevelFailState()` / `Reload()` as `GameEvent` raises. |

`ReportWin()` (`BaseGameManager.cs:135`) is the canonical win path: idempotent guard → analytics →
`IncrementLevel()` → `boOnLevelWin.Invoke()` (the signal the LiveOps dispatcher listens to) → then the
remote-config decision `completedDisplayLevel >= gameConfig.LevelCompletePanelFromLevel ? gotoLevelComplete
: LoadCurrentLevel()`.

### Learning & Design Patterns — **not shipping code**

`Learning/` (`Delegations.cs`, `HighFrequencyFixedUpdate.cs` + their scenes) and
`Design Patterns/Command Pattern/` (`CommandManager`, `ICommand`, `MoveCommand`, `PlayerController`,
`PlayerInputHandler`, `UIController`, `GameManager`, `CommandPattern.unity`) are self-contained teaching
samples. Nothing in the game references them; do not build on them.

---

## Persistence model

Everything persistent in this layer is **PlayerPrefs, keyed by a hand-authored string on each SO asset**.
There is no save file, no encryption, no versioning.

**Where values live.** Each `DB*` asset has `[SerializeField] private string key` — e.g.
`vDBInt_LevelNo` → `LevelNoVariable`, `vDBString_MagicStreakState` → `LiveOp_MagicStreak_State`. The key is
free text; nothing generates or validates it (`KeyRegistry` is commented-out dead code), so **two assets with
the same — or both empty — key silently share one prefs slot.**

**Load timing.** `OnEnable` → `Load()`, which runs when Unity first loads the asset (boot, or the first
inspector/reference touch in the editor) — *not* per scene, not per play session:

```csharp
// DBInt.cs:40  (DBString / DBBool / DBFloat are identical in shape)
private void Load()
{
    if (PlayerPrefs.HasKey(key)) Value = PlayerPrefs.GetInt(key);
    else { Value = DefaultValue; Save(); }   // first run seeds the prefs slot
}
```

**Save timing — the asymmetry that bites.**

| Type | `SetValue` | `Increment`/`Decrement` | `Save()` visibility |
|---|:--:|:--:|---|
| `DBInt` | auto-saves | auto-saves | `private` |
| `DBFloat` | auto-saves | auto-saves | `private` |
| `DBBool` | auto-saves | — | `private` |
| **`DBString`** | **does not save** | — | **`public`** |

`DBString.SetValue` calls only `base.SetValue` (`DBString.cs:13-16`); the value stays in memory until someone
calls `Save()`. Every LiveOp save blob is a `DBString`, which is why the store pairs the two explicitly
(`LiveOps/Architecture/Base/ILiveOpStore.cs:110-111`). **Writing a `DBString` without `Save()` loses the write
on app kill.**

**Hazard — in-memory SO state re-saves over externally cleared prefs.** `Load()` only ever runs on asset
enable. If PlayerPrefs are wiped while the SO is still loaded — `PlayerPrefsUtil.DeleteAll()`
(`LiveOps/RaceEvent/RaceEvent System/Scripts/PlayerPrefsUtil.cs:59`), the PlayerPrefs editor window's
"Delete All", or an external `adb`/registry clear with the editor running — the SO keeps its pre-wipe `Value`,
and the **next** `SetValue`/`Increment` calls `Save()` and writes that stale value straight back. The wipe
appears to partially undo itself, inconsistently, depending on which variables happen to be touched first.
To genuinely reset: clear prefs **and** force the assets to reload (restart the editor / player, or
re-enter play mode with domain reload enabled). `ResetToDefaultOnPlay` does **not** help — `Load()` runs after
the base reset and overwrites `Value` from prefs regardless.

**Hazard — `Value` is a serialized field, so runtime writes dirty the asset in the editor.** `Load()` and
`SetValue` write into `[SerializeField] protected int Value`, so a play session mutates the `.asset` file and
it shows up as a working-tree change. This is visible right now in this repo:

```
Assets/GameKit/LevelSystem/ScriptableObjects/v_LevelNoForText.asset
-  Value: 40
+  Value: 25          # DefaultValue: 1, ResetToDefaultOnPlay: 1 — this is pure play-session drift
```

Never take a `DB*` asset's committed `Value` as meaningful, and don't commit those diffs.

---

## Remote config pipeline

Firebase Remote Config → `RemoteConfigManager` → `FirebaseRemoteFetcherSO` → typed config ScriptableObjects.
Every remote key holds a **JSON string**, and every key has a bundled `TextAsset` default so the game is fully
playable offline and on first launch.

```
OnEnable (FirebaseRemoteFetcherSO.cs:102)
  ├─ RegisterDefaults()                       → RemoteConfigManager.AddOrUpdateValue(key, textAsset.text)
  └─ RemoteConfigManager.OnFetchCompleteWithSuccess += OnRemoteFetchComplete
                                    │
OnRemoteFetchComplete(success, msg)  (:120)   ← failure only logs a warning; defaults stay in place
  ├─ ApplyRemoteSettings()           (:178)
  │    ApplyGameConfig · ApplyAdsConfig · ApplyBoostersConfig · ApplyAbTestConfig · ApplyLevelData
  │    (NumberixThemesConfig and NumberixFeatureUnlockConfig have no Apply* here — see note below)
  └─ OnRemoteFetchedCompleted.Invoke()        ← GameEvent broadcast to the rest of the game
```

| Remote key | Default JSON | Parsed as | Applied into |
|---|---|---|---|
| `NumberixGameConfig` | [GameConfig.json](FirebaseRemoteArchitecture/Json/GameConfig.json) | `GameConfigData` | `GameConfig` SO, `Int minutesPerLife`, seeds `DBInt softCurrency` |
| `NumberixAdsConfig` | [AdsConfig.json](FirebaseRemoteArchitecture/Json/AdsConfig.json) | `AdsConfigData` | `AdsRemoteSettingsSO`, `InAppsConfiguration`, `RemoveAdsState` |
| `NumberixBoostersConfig` | [BoostersConfig.json](FirebaseRemoteArchitecture/Json/BoostersConfig.json) | `BoostersConfigWrapper` | each `BoosterDefinition` in `BoosterDatabaseSO`, by `boosterId` |
| `NumberixFeatureUnlockConfig` | FeatureUnlockConfig.json | game-side | **Not applied by this fetcher.** `FirebaseRemoteFetcherSO` only registers the default and fetches the raw string; the apply runs game-side (Assembly-CSharp, `TutorialUnlockRemoteConfig`), off `OnRemoteFetchedCompleted`, because the kit can't reference the game's tutorial database. |
| `NumberixThemesConfig` | ThemesConfig.json | game-side | **Not applied by this fetcher**, for the same reason — the apply runs game-side (`ThemeRemoteConfig`/`ThemeBootstrap`) off `OnRemoteFetchedCompleted`. |
| `NumberixLevelData` | `Leveldata.json.txt` | `LevelDataWrapper` (Newtonsoft) | `LevelsData` SO — `Clear()` then `AddLevel(level, type)` |
| `abtest` | [AbTestConfig.json](FirebaseRemoteArchitecture/Json/AbTestConfig.json) | `AbTestConfigData` | `SharedString ABTest`, `Int ABGroup`, `SharedString ABTestStartDate` |

⚠️ **The live-ops config path no longer exists.** `BlockLiveOpsConfig` → `ApplyLiveOpsConfig()` /
`LiveOpsConfigData`, and the tray config path `BlockTrayConfig` → `TrayConfig.json` →
`TrayExpansionConfigSO.ApplyRemoteConfig(...)`, are both deleted along with their JSON files —
Numberix ships no live-ops and no tray expansion. `LiveOpsEntry` itself survives in
`FirebaseRemoteFetcherSO.cs` only because `LiveOpBaseConfig` subclasses elsewhere in `GameKit.Runtime`
still take one; it has no remote key feeding it in this project.

Each LiveOps entry is a flat `LiveOpsEntry` (same shape as `BaseLifeOpsData`) copied field-for-field into a
`LiveOpBaseConfig` (`LiveOps/Architecture/Base/LiveOpBaseConfig.cs:14`):
`unlockLevel → UnlockLevel`, `timeToExpire → Duration`, `sortOrder → SortOrder`,
`onboardingSortOrder`, `expireSortOrder`, `popupThreshold`.

### Adding a new remote-tunable field

1. **Add the field to the data contract** in `FirebaseRemoteFetcherSO.cs:715-862` — e.g. a new `int` on
   `GameConfigData`. The C# name must match the JSON key **exactly** (`JsonUtility`, case-sensitive).
2. **Add the key to the bundled default JSON** in `FirebaseRemoteArchitecture/Json/`. If it is missing there,
   the field parses to `0`/`false`/`null` on every offline run.
3. **Add the target field** to the destination config SO (`GameConfig`, `AdsRemoteSettingsSO`, …).
4. **Assign it in the matching `Apply*` method**, and **guard numeric fields that must not be zeroed**:
   ```csharp
   // FirebaseRemoteFetcherSO.cs:233 — a payload omitting the key parses to 0 and would wipe the SO default
   if (cfg.energyRewardedReviveAmount > 0) mazeGameConfigSO.EnergyRewardedReviveAmount = cfg.energyRewardedReviveAmount;
   ```
5. **Append a line to that method's `StringBuilder` log** — the `[FirebaseFetcher]` block is the only way to
   verify on-device what actually landed.
6. **Verify without a live fetch** with the `ApplyDefaultsToScriptables` Odin `[Button]` (`:170`), which runs
   `RegisterDefaults()` + `ApplyRemoteSettings()` from the bundled JSON.
7. Finally, add the key in the Firebase console with a value matching the bundled default.

**There is no "add a new LiveOp" path in this project.** `LiveOpsConfigData`, `LiveOpsConfig.json` and
`ApplyLiveOpsConfig` — the steps this section used to describe (a `LiveOpsEntry` field, a JSON block, a
serialized `LiveOpBaseConfig` reference, an `if (cfg.X != null && xConfigSO != null)` block) — were all
**deleted**; Numberix ships no live-ops remote config. See the
[`IRemoteLiveOpsFetcher` is bypassed](#rules--gotchas) gotcha below, which is the corrected version of
this paragraph.

---

## Rules & gotchas

- **`GameEvent` nulls its handler list in `OnDisable`.** MonoBehaviour subscribers are fine (they re-attach in
  `OnEnable`). **ScriptableObject subscribers are not** — they need a public, idempotent detach-then-attach
  `Initialize()` that a scene-driven caller re-asserts (the LiveOps dispatcher does this from every home
  entry). A "the event just stopped firing after a while" bug is almost always this.
- **`GameEvent.Invoke` isolates each subscriber; `GameEventWithParam<T>.Raise` does not.** A thrower on a typed
  event still kills every later subscriber in the invocation list, and typed events don't clear handlers on
  disable either — the two families genuinely behave differently.
- **`DBString.SetValue` does not persist. Always pair it with `Save()`.** Every other `DB*` type auto-saves,
  which makes the exception easy to miss.
- **Clearing PlayerPrefs at runtime does not clear loaded SOs**, and the next write puts the stale value back.
  See [Persistence model](#persistence-model).
- **`DB*` keys are hand-typed and unvalidated.** Empty or duplicated keys collide on one prefs slot — this
  exact bug hit the LiveOps save blobs and was fixed in 2026-07 by giving each a distinct
  `LiveOp_<Id>_State` key.
- **Play sessions dirty `v*.asset` files** because `Value` is serialized. Check `git status` before committing.
- **`ResetToDefaultOnPlay` is meaningless on `DB*` types** — `Load()` runs after it and overwrites `Value`
  from prefs (or re-seeds `DefaultValue` and saves).
- **`DBIntWithEvent` / `DBStringWithEvent` / `DBBoolWithEvent` call `GameEvent.Invoke()` unguarded.** Leave the
  event slot empty and every write throws a `NullReferenceException`.
- **`SharedDictionary.OnEnable` wipes the dictionary.** It is runtime scratch storage only; authored contents
  will not survive.
- **A remote payload that omits a key parses to `0`/`false`.** Guard any `Apply*` assignment whose SO default
  must survive an incomplete payload; several already are (`:233`, `:242`, `:250`).
- **`FirebaseRemoteFetcherSO` uses `JsonUtility` for everything except level data** (Newtonsoft). `JsonUtility`
  cannot parse a top-level array, ignores properties, and is case-sensitive on field names.
- **`IRemoteLiveOpsFetcher` is bypassed, and there is no live-ops remote config in this project at
  all.** The fetcher used to hold ten *concrete* `LiveOpBaseConfig` references and call
  `ApplyLiveOpsData(LiveOpsEntry)` directly for each one (`ApplyLiveOpsConfig`) — that method,
  `LiveOpsConfigData` and `LiveOpsConfig.json`/`BlockLiveOpsConfig` are all **deleted**; Numberix
  ships no live-ops. `LiveOpsEntry` itself is kept only because other `LiveOpBaseConfig` subclasses
  in `GameKit.Runtime` still take one. See the
  [LiveOps remote-config caveat](../LiveOps/ARCHITECTURE.md#5-capability--type-matrix).
- **`PoolManagerSO.Get` returns `null` (logged, not thrown) for an unknown pool id** — null-check the result.
  `Release` with an unknown id `Destroy`s the object instead of pooling it.
- **`ServerTimeService.FetchServerTime` is re-entrancy-guarded and silently `yield break`s** if a fetch is
  already in flight — neither callback runs, so callers must tolerate no reply.
- **Two `TimeMachine` classes exist** (`ProjectCore.TheTimeMachine` — the one the project uses — and
  `THEBADDEST.Coroutines`). Always qualify the namespace.
- **Known dead / stubbed code:** `Variables/Helper/KeyRegistry.cs` (fully commented out),
  `AdPackage/Scripts/IAdsService.cs` (no implementers), `Utilities/PoolSystem/Scripts/PoolMonitor.cs` (body
  commented out), `MonetizationAdsService.ExtendInterstitialTime()` (explicit no-op),
  `MeshCombiner`'s `disableOriginalRenderers` parameter, `FirebaseRemoteArchitecture/Json/Old/`, and the whole
  `Learning/` and `Design Patterns/` folders.
