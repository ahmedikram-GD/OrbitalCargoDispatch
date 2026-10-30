# GameKit Haptics — `HapticHandlerSO`

`namespace ProjectCore.Systems.HapticsSystem` · root `Assets/GameKit/Haptics/` · 1 script, 1 asset

**One-line model:** a single ScriptableObject that maps a project-local `HapticIntensity` enum onto Lofelt
Nice Vibrations presets, gated by one global on/off variable. That is the entire module.

---

## Mental model

There is exactly **one** `HapticHandlerSO` asset in the project. Every view/widget that wants a buzz holds a
serialized reference to it and calls `ApplyHapticFeedback(HapticIntensity.X)`. The SO is stateless: it checks the
`vibrations` toggle, switches on the enum, and forwards to `HapticPatterns.PlayPreset`. No queueing, no cooldown,
no device-capability check (Lofelt handles that internally).

## Key types

| Type | File | Role |
|---|---|---|
| `enum HapticIntensity` | [Scripts/HapticsHandler.cs:7](Scripts/HapticsHandler.cs#L7) | `Selection · Light · Soft · Medium · Hard · Success · Failure` — the project-facing vocabulary. |
| `HapticHandlerSO` | [Scripts/HapticsHandler.cs:19](Scripts/HapticsHandler.cs#L19) | `ScriptableObject`. Two public methods: `ApplyHapticFeedback(HapticIntensity)` and `ApplyDynamicHaptic(float)`. |

### API

```csharp
public void ApplyHapticFeedback(HapticIntensity intensity)  // HapticsHandler.cs:26
public void ApplyDynamicHaptic(float intensity01)           // HapticsHandler.cs:63  — DEAD, no call sites
```

Enum → Lofelt preset mapping (`HapticsHandler.cs:33-60`):

| `HapticIntensity` | `HapticPatterns.PresetType` |
|---|---|
| `Selection` | `Selection` |
| `Light` | `LightImpact` |
| `Soft` | `SoftImpact` |
| `Medium` | `MediumImpact` |
| `Hard` | `HeavyImpact` |
| `Success` / `Failure` | `Success` / `Failure` |

## How it is used

**40 call sites across 31 files.** All follow one of three patterns.

**1 — Button/tap feedback (the overwhelming majority, ~33 of 40).** A `[SerializeField] HapticHandlerSO` on a view,
fired next to the click SFX:

```csharp
// HomeSettingsView.cs:49 · ShopView.cs:57 · SettingsView.cs:53 · RetryView.cs:35 · LeaveView.cs:38
// GiveUpView.cs:25 · RemoveAdsView.cs:34,:71 · BuyBoosterView.cs:41 · LevelCompleteView.cs:88 …
hapticHandlerSO?.ApplyHapticFeedback(HapticIntensity.Selection);
```

**2 — Shared base class.** The LiveOps home icons get it for free instead of each declaring their own field:

```csharp
[SerializeField] protected HapticHandlerSO hapticHandlerSO;              // LiveOpIconBase.cs:17
hapticHandlerSO?.ApplyHapticFeedback(HapticIntensity.Selection);         // LiveOpIconBase.cs:47
```
Subclasses that still fire it themselves: `RaceIcon.cs:84`, `BroomStreakIcon.cs:77`, `DailyChallengeIcon.cs:52`,
`RemoveAdIcon.cs:38`.

**3 — Animation/impact accents (the only non-`Selection` uses).**

| Call site | Intensity | Trigger |
|---|---|---|
| `Art/UI/Scripts/Header/LivesWidget.cs:152, :182` | `Medium` | pulse-tween complete, hit particles |
| `Art/UI/Scripts/Header/CoinsWidget.cs:135` | `Medium` | coin count-up impact |
| `AppFlow/States/MovesWarning/Scripts/WarningMovesView.cs:102` | `Medium` | glow fade-in `OnComplete` |
| `UIAnimations/CoinTrail/Scripts/UICollector.cs:114` | `Soft` | each coin lands |
| `UIAnimations/CoinTrail/Scripts/UICollector.cs:143` | `Hard` | trail finishes |
| `AppFlow/States/BoosterUnlocked/BoosterOnboard/BoostersOnBoardingHandler.cs:234` | `Hard` | booster reveal |

**4 — Configurable intensity (one instance).** Only `GameSettings` exposes the enum to the inspector:

```csharp
[SerializeField] private HapticIntensity _hapticIntensity = HapticIntensity.Selection;  // GameSettings.cs:18
_hapticHandler?.ApplyHapticFeedback(_hapticIntensity);                                  // GameSettings.cs:41
```

## Configuration

**`Scriptables/HapticHandlerSO.asset`** — the only instance. One serialized field:

| Field | Type | Wired to |
|---|---|---|
| `vibrations` | `DBBoolWithEvent` | `Assets/GameKit/Art/Sounds/Scriptables/Vibrations.asset` (PlayerPrefs key `VibrationsVariable`, default `true`) |

The gate lives outside this folder, next to the sound settings — the settings UI flips `Vibrations` directly; the
handler only reads it. Both public methods early-return when `vibrations.GetValue()` is false
(`HapticsHandler.cs:28`, `:65`).

## Rules & gotchas

- **`ApplyDynamicHaptic` is dead.** Zero call sites project-wide. It maps a 0–1 float onto five presets and is
  the only place `RigidImpact` is ever referenced.
- **Three enum values are never used:** `Light`, `Success`, `Failure`. Shipping code only emits `Selection`,
  `Medium`, `Hard`, `Soft`.
- **`vibrations` is not null-checked.** `HapticsHandler.cs:28` calls `vibrations.GetValue()` unguarded — an
  unassigned reference is an NRE on the first tap, not a silent no-op. Call sites null-check the *handler*
  (`hapticHandlerSO?.`), which does not help here.
- **File/class name mismatch.** The file is `HapticsHandler.cs`; the class is `HapticHandlerSO`. Searching for
  `HapticHandlerSO.cs` finds nothing.
- **No throttling.** `UICollector.cs:114` fires `Soft` per landed coin — a large trail produces a burst of
  back-to-back presets.
- **Adding an intensity is a two-file change**: extend the enum *and* the `switch`. An unmapped value falls into
  `default:` and only logs `"[Haptic] Unknown haptic intensity."` (`HapticsHandler.cs:58`).
- Every consumer holds its own inspector reference — there is no service locator. A new prefab that forgets to
  assign the field is silently haptic-free (except `GameSettings`/`ToggleSO`-style sites, which use `?.`).
