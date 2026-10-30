using GameKit.AppFlow;
using GameKit.Architecture.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalCargo.UI
{
    /// <summary>
    /// The shift HUD (pooled "ShiftHud", spawned by <c>ShiftGameState</c>): credits (kit CoinsWidget +
    /// coin trail, wired in the prefab), shift label, mm:ss timer and the pause button. Passive: the
    /// state binds the button; the timer just mirrors e_ShiftTick. The kit <see cref="GameHud"/> owns
    /// OnEnable/OnDisable, so the tick subscription lives in Awake/OnDestroy (one per instance).
    /// </summary>
    [AddComponentMenu("OrbitalCargo/UI/Hud View")]
    public sealed class HudView : GameHud
    {
        [Header("Shift")]
        [Tooltip("e_ShiftTick (int) — whole seconds remaining, raised by ShiftController.")]
        [SerializeField] private GameEventWithInt onShiftTick;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text shiftText;
        [SerializeField] private Button pauseButton;

        [Header("Look")]
        [Tooltip("Seconds left when the timer turns to the warning colour.")]
        [Min(0)] [SerializeField] private int warningSeconds = 30;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color warningColor = new Color(1f, 0.35f, 0.25f);
        [Tooltip("Shift label format; {0} = shift number.")]
        [SerializeField] private string shiftFormat = "SHIFT {0}";

        public Button PauseButton => pauseButton;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();
            if (onShiftTick != null) onShiftTick.Subscribe(HandleShiftTick);
        }

        private void OnDestroy()
        {
            if (onShiftTick != null) onShiftTick.UnSubscribe(HandleShiftTick);
        }

        // ── Public API ─────────────────────────────────────────────────────────

        public void SetShiftNumber(int number)
        {
            if (shiftText != null) shiftText.text = string.Format(shiftFormat, number);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void HandleShiftTick(int secondsRemaining)
        {
            if (timerText == null) return;
            int s = Mathf.Max(0, secondsRemaining);
            timerText.text = $"{s / 60}:{s % 60:00}";
            timerText.color = s <= warningSeconds ? warningColor : normalColor;
        }
    }
}
