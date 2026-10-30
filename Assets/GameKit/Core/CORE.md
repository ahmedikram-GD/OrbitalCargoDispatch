# GameKit Core

Three unrelated shared seams: the booster DI interfaces, the pluggable fail framework, and a shake utility.

`Assets/GameKit/Core/` · namespaces `Blues.Core` (DI, Scripts) and `Blues.Core.Game.Fail` (Fail) · 14 scripts, no asmdef (all compile into `Assembly-CSharp`).

## Mental model

Core is not one system — it is three small folders that happen to share a root. **`Fail/`** is the real
subsystem: a `ScriptableObject` registry (`FailService`) that conditions self-register with, a monitor that
arms/disarms them on the level lifecycle, and a string `reasonId` that travels from the tripped condition all
the way to the fail panel and analytics. **`DI/`** is two interfaces that break the GameManager ⇄ BoosterHUD
reference cycle. **`Scripts/`** is a shake helper plus two injection interfaces, and is effectively dormant
today (see the table).

## Key types

| Type | File | Role |
|---|---|---|
| `IBoosterHUD` | [DI/IBoosterHUD.cs](DI/IBoosterHUD.cs) | What gameplay needs from the booster HUD: `SetTargetingMode`, `RefreshAll`, `UnlockBooster`, button lookups. Registered via `RegisterInstance` (never `RegisterComponent`) so `[Inject]` doesn't fire and re-create the cycle. |
| `IBoosterService` | [DI/IBoosterService.cs](DI/IBoosterService.cs) | The other half of the cycle: `ActivateBooster(BoosterSO, Vector3)` / `CancelActiveBooster()`. Implemented by `BoosterRunner` in GameKit. |
| `FailService` | [Fail/FailService.cs](Fail/FailService.cs) | `ScriptableObject`, `IFailSink`. Condition registry + single-fire latch + `LastReason`. Fires `levelLostTrigger` and disarms everything on the first report. Asset: `Fail/FailService.asset`. |
| `IFailCondition` | [Fail/IFailCondition.cs](Fail/IFailCondition.cs) | One "the level fails when X" rule: `Id`, `Arm(IFailSink)`, `Disarm()`. |
| `IFailSink` | [Fail/IFailSink.cs](Fail/IFailSink.cs) | What conditions report *to* — `IsArmed`, `ReportFail(FailReason)`. Conditions depend on this, never on `FailService`. |
| `FailReason` | [Fail/FailReason.cs](Fail/FailReason.cs) | `readonly struct` — `Id` (stable key) + optional `Message`. |
| `FailConditionMonitor` | [Fail/FailConditionMonitor.cs](Fail/FailConditionMonitor.cs) | The only lifecycle wiring: level-start / continue / continue-rewarded → `ArmAll()`, win → `DisarmAll()`. |
| `EventFailCondition` | [Fail/EventFailCondition.cs](Fail/EventFailCondition.cs) | Instant fail on a `GameEvent`. `reasonId` and `message` are inspector fields — reusable for any "blast"-style fail without new code. Default `"BombExploded"`. |
| `TimeoutFailCondition` | [Fail/TimeoutFailCondition.cs](Fail/TimeoutFailCondition.cs) | Countdown fail (`Id => "Timeout"`), optional `TMP_Text` MM:SS label. Countdown starts in `Arm`, stops in `Disarm`. |
| `FailReasonVisual` | [Fail/FailReasonVisual.cs](Fail/FailReasonVisual.cs) | `[Serializable]` per-reason presentation: `reasonId`, `mainIcon`, `rewardIcon`, `title`, `detail`. |
| `FailReasonVisualSet` | [Fail/FailReasonVisualSet.cs](Fail/FailReasonVisualSet.cs) | `ScriptableObject` list of the above with `TryGet(reasonId, out visual)` (linear scan). Asset: `Fail/FailReasonVisualSet.asset`. |
| `TransformShake` | [Scripts/TransformShake.cs](Scripts/TransformShake.cs) | Local-position shake coroutine; overlapping calls blend (max intensity, max end time). **Live type but no live callers** — nothing in the project calls `Shake(...)`, and no prefab references the component. |
| `IShakeInjectable` | [Scripts/IShakeInjectable.cs](Scripts/IShakeInjectable.cs) | **Unimplemented.** Zero classes implement it; the only consumer is a type test at `BoosterButtonView.cs:213` that therefore never runs. |
| `IParticleInjectable` | [Scripts/IParticleInjectable.cs](Scripts/IParticleInjectable.cs) | **Dead file** — contains only a two-line removal comment, declares no type. |

## How it is used

**Booster DI, without VContainer.** `BoosterHudBootstrap` is the no-DI wiring path used by the shipping HUD
prefabs; it resolves both interfaces by hierarchy search and calls the manual `Construct()` methods
(`BoosterHudBootstrap.cs:45-61`):

```csharp
var runner = hudRoot.GetComponentInChildren<BoosterRunner>(true);   // the single IBoosterService
...
_hud.Construct(runner, highlight, panel, tutorial, shake);
runner.Construct(_hud);                                             // IBoosterHUD back into the runner
```

The manual-`Construct` dance is the whole point of the folder — `BoosterHUDController.cs:30-38` spells out the
cycle it avoids. The booster pipeline then passes `IBoosterHUD` down into every selection/onboarding phase
(`IBoosterSelection.cs:28`, `IBoosterOnboardingPhase.cs:20`, `DefinitionDrivenOnboarding.cs:24`).

**Fail conditions are composed, not coded.** `EnergyDrainController` (a game-side script, not in Core)
implements `IFailCondition` and reports through the sink (`EnergyDrainController.cs:23`, `:85`, `:125`):

```csharp
public class EnergyDrainController : MonoBehaviour, IFailCondition
public void Arm(IFailSink sink) => _sink = sink;
    _sink.ReportFail(new FailReason("OutOfEnergies", "Out of energy!"));
```

**The reason id drives presentation.** `LevelFailState` reads `FailService.LastReason` as the single source of
truth, resolves visuals from the set, and maps the id onto its panel enum (`LevelFailState.cs:76`, `:111`,
`:117-124`):

```csharp
if (failReasonVisuals != null && failReasonVisuals.TryGet(ReasonId(), out var v))
...
case "BombExploded": return LevelFailReason.BombExploded;
case "Timeout":      return LevelFailReason.Timeout;
default:             return LevelFailReason.OutOfEnergies;
```

**And analytics.** `MeticaAnalyticsSystem.cs:31`, `:217-224` reads the same `LastReason.Id` and maps it to an
event label, falling back to logging the raw id verbatim for unknown reasons.

**Scene wiring.** `FailConditionMonitor`, `EventFailCondition` and `TimeoutFailCondition` are each present in
exactly two prefabs: `Assets/GameKit/AppFlow/States/BO_Game/GameHUD/Prefabs/ArchitectureTesting_GameHud.prefab`
and `Assets/YarnGame/Game/Resources/YarnSort/BO_GameHud_Yarn.prefab`.

## Rules & gotchas

- **`ArmAll()` is the only reset.** `_tripped` and `LastReason` clear in `ArmAll` (`FailService.cs:48-56`),
  never in `DisarmAll`. So `LastReason` deliberately survives the fail (that's how the panel and analytics read
  it), but it also survives *into the next level* until something arms. If the level-start `GameEvent` on
  `FailConditionMonitor` is unwired, the previous attempt's reason is what the fail panel shows.
- **Arming is a snapshot.** `ArmAll` iterates whoever is registered *at that moment*. A condition whose
  GameObject is enabled after arming registers but never receives its sink, so `_sink` stays null and it can
  never fail the level. `TimeoutFailCondition` is worse — its countdown only starts inside `Arm`.
- **Reason ids are magic strings in four places** with no shared constant: the condition
  (`TimeoutFailCondition.cs:27`, `EventFailCondition.reasonId`), the `LevelFailState` switch
  (`LevelFailState.cs:121-123`), the Metica switch (`MeticaAnalyticsSystem.cs:220-223`), and the
  `FailReasonVisualSet` entries. A new id silently falls through to the `OutOfEnergies` panel while still
  logging correctly to analytics — the two fallbacks disagree.
- **`levelLostTrigger` must be the same `GameEvent` asset as `BaseGameManager.levelLostTrigger`**
  (`e_TemplateLevelLost`), per the tooltip at `FailService.cs:22-23`. Wrong asset = condition trips, latch sets,
  nothing happens on screen.
- **`FailService` is a shared SO across HUD/gameplay/state assets.** Runtime state is `[NonSerialized]`
  specifically so editor play-sessions and domain reloads don't carry a stale latch — do not add serialized
  runtime fields here.
- **`EventFailCondition.Disarm()` is intentionally a no-op** (`:47`). Post-win safety comes only from the
  `_sink.IsArmed` guard in `Trigger()`, so a condition that reports without checking `IsArmed` will fail a level
  the player already won.
- **`PruneDestroyed` only prunes Unity objects** (`c is Object o && o == null`). A plain-C# `IFailCondition`
  that goes away must call `Unregister` itself.
- **The Yarn game has its own parallel `IBoosterService` / `IBoosterHUD`** in namespace `Game.YarnSort`
  (`Assets/YarnGame/Game/Screens/YarnSort/Scripts/Boosters/DI/`). Same names, different types — grep results are
  misleading, and `Assets/GameKit/AppFlow/.../BoostersOnBoardingHandler.cs:17` documents against the *Yarn* one.
- **`Scripts/` is close to dead weight.** `TransformShake` has no prefab reference anywhere, so
  `BoosterHudBootstrap.cs:48-49` always hits its `AddComponent` fallback — which leaves `_gridRoot` unassigned,
  making the parameterless `Shake(intensity, duration)` overload a permanent no-op. It doesn't matter yet
  because nothing calls `Shake` at all. Treat `IShakeInjectable` / `IParticleInjectable` as removal candidates,
  not as extension points.
