# GameKit UI

The shared in-game overlay layer: the one-time-tutorial contract, the booster targeting/onboarding overlay, and the energy HUD widget.

`Assets/GameKit/UI/` · namespace `Blues.Core.UI` · 7 scripts, all under `Onboarding/Scripts/`

> **Mostly assets.** Of this folder's eight subdirectories — `Art`, `Materials`, `Prefabs`, `ScreenRedFlash`,
> `fonts`, `mocks`, `Onboarding` — only `Onboarding/Scripts/` contains code. Everything else is sprites,
> materials, fonts and prefabs. There is no asmdef; these types compile into `Assembly-CSharp`.

## Mental model

Two independent things live here. **Tutorials** are a discovery contract, not a framework: a tutorial is any
MonoBehaviour under the HUD implementing `ITutorial`, and a director elsewhere finds it with
`GetComponentsInChildren` and asks it whether it wants to show. **The booster overlay** is three dumb view
components (`TutorialCanvasManager`, `BoosterHighlightManager`, `PreBoosterSelectionPanel`) that only ever get
driven by `BoosterHUDController` — none of them knows what a booster is. `EnergyHUDController` is filed here by
accident of folder layout; it is a pure animation widget for the HUD's energy slots, unrelated to onboarding.

## Key types

| Type | File | Role |
|---|---|---|
| `ITutorial` | [Onboarding/Scripts/ITutorial.cs](Onboarding/Scripts/ITutorial.cs) | Contract for a one-time tutorial: `HasBeenSeen`, `IsShowing`, `ShouldShow(displayLevel)`, `TryShow()`, `Dismiss()`. Each tutorial encodes its own level trigger so directors need no per-prefab wiring. |
| `ITutorialFocus` + `TutorialFocus` | [Onboarding/Scripts/ITutorialFocus.cs](Onboarding/Scripts/ITutorialFocus.cs) | `EnterFocus()` / `ExitFocus()` — the *game* implements this so a fullscreen tutorial can dim behind the board and hide tray + footer without referencing game types. `TutorialFocus.Active` is a static mutable service-locator field. |
| `FirstMoveTutorialController` | [Onboarding/Scripts/FirstMoveTutorialController.cs](Onboarding/Scripts/FirstMoveTutorialController.cs) | The only `ITutorial` implementation inside GameKit. Snaps the HUD off on show, dismisses on the first valid block tap, persists to a `DBBool`, then destroys itself. |
| `TutorialCanvasManager` | [Onboarding/Scripts/TutorialCanvasManager.cs](Onboarding/Scripts/TutorialCanvasManager.cs) | Swaps `hudElementsHolder` ⇄ `tutorialsCanvas` (siblings in one canvas) so tutorial UI is interactive while the normal HUD is off. `Show()` / `Hide()` / `ForceHide()`. |
| `BoosterHighlightManager` | [Onboarding/Scripts/BoosterHighlightManager.cs](Onboarding/Scripts/BoosterHighlightManager.cs) | Fades a full-screen dim in and activates exactly one duplicate booster slot above it. Raises `OnHighlightedBoosterClicked(int)`. |
| `PreBoosterSelectionPanel` | [Onboarding/Scripts/PreBoosterSelectionPanel.cs](Onboarding/Scripts/PreBoosterSelectionPanel.cs) | The "Hammer — select a block" header shown during targeting. Fades in/out, raises `OnCloseClicked`. Knows nothing about targeting beyond a `BoosterDefinitionSO` overload. |
| `EnergyHUDController` | [Onboarding/Scripts/EnergyHUDController.cs](Onboarding/Scripts/EnergyHUDController.cs) | Drives the 3 energy slots: consume punch, level-entry staggered reveal, and a centre-spawn "fly in" for revive gains. Pure presentation — holds no energy count. Misfiled under `Onboarding/`. |

## How it is used

**Tutorials are auto-discovered, never wired.** `TutorialDirector` (template game) scans the HUD root on every
level start (`TutorialDirector.cs:55-57`, `:70-72`):

```csharp
foreach (var mb in HudRoot.GetComponentsInChildren<MonoBehaviour>(true))
    if (mb is ITutorial t && !t.HasBeenSeen && t.ShouldShow(lvl))
        t.TryShow();
```

`YarnSortBootstrap.cs:444` and `:454` contain the same two loops independently. Implementations outside this
folder: `PinchZoomTutorialController.cs:26`, `YarnSortOnboardingTutorial.cs:31`, `TrayExpandTutorial.cs:28`.

**Fullscreen tutorials borrow the game's focus.** The pinch tutorial stays game-agnostic by going through the
static (`PinchZoomTutorialController.cs:133`, `:145`):

```csharp
TutorialFocus.Active?.EnterFocus();
...
TutorialFocus.Active?.ExitFocus(); // remove the dim, restore tray + footer
```

The Yarn `GameController` is the implementor and registers/clears itself (`GameController.cs:28`, `:277`,
`:361-362`).

**The booster overlay is assembled by hierarchy search, then driven by one controller.**
`BoosterHudBootstrap.cs:54-57` collects all three views and hands them over in a single `Construct`; the HUD
then sequences them (`BoosterHUDController.cs:141-163`):

```csharp
tutorialCanvas?.Show();          // swap to the tutorials holder FIRST
if (boosterIndex >= 0) highlightManager?.Show(boosterIndex);
if (definition  != null) selectionPanel?.Show(definition);
```

Teardown is the strict reverse — panels hide, *then* `tutorialCanvas?.Hide()`, then every button refreshes.

**Energy HUD has two callers with different jobs.** `DefaultGameHud` owns the reveal choreography — it is
handed the controller at `BoosterHudBootstrap.cs:65-66` (`boHud.SetEnergyHud(energy)`) and calls
`PrepareLevelEntry()` before bars fade and `PlayLevelEntryReveal(animate: true)` after a lead-in
(`DefaultGameHud.cs:97`, `:125`, `:133`). The *count* comes from gameplay: `EnergyDrainController.cs:122` calls
`OnChancesChanged(_current)` on each drain and `:109` calls `RefreshToCount(_current, animate: true)` on
refill.

**Prefab homes.** All of `TutorialCanvasManager`, `BoosterHighlightManager`, `PreBoosterSelectionPanel` and
`EnergyHUDController` appear in exactly two prefabs — `ArchitectureTesting_GameHud.prefab` and
`BO_GameHud_Yarn.prefab`. `FirstMoveTutorialController` lives only in
`Assets/GameHudTutorials/FirstMoveTutorial/Prefabs/FirstMoveTutorial.prefab`.

## Rules & gotchas

- **`FirstMoveTutorialController.Dismiss()` destroys its own GameObject** (`:88`). It is single-use per scene
  load; nothing can re-show it, and any later `DismissAllShowing()` sweep simply won't find it.
- **Its `_seenFlag` `DBBool` is shared with app boot.** `GameFlow.cs:101-103` reads the same asset to decide
  whether to skip Home and boot straight into gameplay on first install. Flipping or clearing that flag for
  tutorial testing changes the startup route.
- **The first-move tutorial never blocks input.** `SetVisible` always sets `interactable = false` and
  `blocksRaycasts = false` (`:110-113`) — it is an overlay the player taps *through*, and dismissal comes from
  the `_onValidBlockAction` GameEvent, gated by `_minDisplaySeconds`.
- **`TutorialCanvasManager.cs:72` dereferences `_footerSetter` unguarded** while every other reference in the
  class uses `?.` — an unassigned footer setter NREs on the first `Show()`, i.e. the first time a targeting
  booster is used.
- **The `TutorialCanvasManager` summary comment is stale.** It describes two separate canvases at Order −1 / 0;
  the code (and the tooltip at `:35-38`) toggles two sibling holders inside a single canvas. Trust the tooltip.
- **`PreBoosterSelectionPanel.Awake` dereferences `panelRoot` without a null check** (`:59`) — an unwired
  `panelRoot` throws before anything else initialises.
- **`BoosterHighlightManager.boosterSlots[]` order is load-bearing** and must match the real footer order
  (Hint, Hammer, Rocket): the index comes straight from `IBoosterHUD.SetTargetingMode(..., boosterIndex, ...)`.
  Out-of-range indices only log a warning and no-op (`:82`). Its `Awake` adds button listeners and never
  removes them.
- **`Hide()` does not deactivate the manager's own GameObject** even though `Show()` activates it (`:85`); only
  the overlay and the slots are turned off.
- **`EnergyHUDController` calls `StopAllCoroutines()` in four public methods** (`OnChancesChanged`,
  `RefreshToCount`, `PrepareLevelEntry`, `PlayLevelEntryReveal`). Any in-flight animation on this component
  dies mid-frame, which is exactly why `ConsumeSlot` re-asserts base position/scale/active defensively at
  `:372-384`. Do not host unrelated coroutines on this component.
- **Guard on `_baseRects`, not `_slots`.** `_slots` is serialized so it is non-null even when the GameObject was
  never activated (Yarn disables the energy section); `_baseRects` is built in `Awake`. The comment is repeated
  at `:159`, `:178`, `:239` — indexing without it NREs.
- **Mixed time bases.** Entry reveal, spawn and landing animations use `Time.unscaledDeltaTime`, but
  `ConsumeSlot` uses `Time.deltaTime` (`:389`, `:413`) — the consume punch freezes at `timeScale == 0` while
  every other animation keeps playing.
- **`OnChancesChanged(remaining)` uses `remaining` as the index of the slot just consumed** (`:158-161`), which
  is only correct while slots are spent highest-index-first.
- **`TutorialFocus.Active` is a static mutable field with no ownership guarantees.** `GameController` clears it
  only when it is still the registered instance (`GameController.cs:361-362`); a second game controller
  overwriting it would leave the first orphaned.
