using System.Collections.Generic;
using GameKit.Architecture.Events;
using OrbitalCargo.Radio;
using TMPro;
using UnityEngine;

namespace OrbitalCargo.UI
{
    /// <summary>
    /// Rook's radio card on the HUD: speaker tag over a typewriter line (pattern from Silly Justice
    /// <c>TaskBriefTickerUI</c>). Hears e_RadioBeat, looks the beat up in the radio script and queues it,
    /// so lines that land close together play one after another instead of stomping. Scaled time:
    /// a paused game pauses the radio too.
    /// </summary>
    [AddComponentMenu("OrbitalCargo/UI/Radio Line View")]
    public sealed class RadioLineView : MonoBehaviour
    {
        [Tooltip("RadioScript_Pitch.")]
        [SerializeField] private RadioScriptSO script;
        [Tooltip("e_RadioBeat (string).")]
        [SerializeField] private GameEventWithString onRadioBeat;

        [Header("View")]
        [Tooltip("The card (fades in and out as a whole).")]
        [SerializeField] private CanvasGroup card;
        [SerializeField] private TMP_Text speakerText;
        [SerializeField] private TMP_Text lineText;

        [Header("Timing")]
        [Min(0f)] [SerializeField] private float fadeSeconds = 0.2f;
        [Tooltip("Letters typed per second.")]
        [Min(1f)] [SerializeField] private float charsPerSecond = 40f;
        [Tooltip("Lines waiting beyond this are dropped (oldest first) so the radio never lags far behind play.")]
        [Min(1)] [SerializeField] private int maxQueued = 3;

        private readonly Queue<RadioLine> _queue = new Queue<RadioLine>();
        private RadioLine _current;
        private float _typed;
        private float _holdLeft;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (card != null) card.alpha = 0f;
        }

        private void OnEnable()
        {
            if (onRadioBeat != null) onRadioBeat.Subscribe(HandleRadioBeat);
        }

        private void OnDisable()
        {
            if (onRadioBeat != null) onRadioBeat.UnSubscribe(HandleRadioBeat);
            _queue.Clear();
            _current = null;
            if (card != null) card.alpha = 0f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_current == null && _queue.Count > 0) Begin(_queue.Dequeue());

            float target = _current != null ? 1f : 0f;
            if (card != null)
                card.alpha = fadeSeconds > 0f ? Mathf.MoveTowards(card.alpha, target, dt / fadeSeconds) : target;

            if (_current == null) return;

            string text = _current.Text ?? string.Empty;
            if (_typed < text.Length)
            {
                _typed += charsPerSecond * dt;
                if (lineText != null) lineText.maxVisibleCharacters = Mathf.Min(text.Length, Mathf.FloorToInt(_typed));
                return;
            }

            _holdLeft -= dt;
            if (_holdLeft <= 0f) _current = null; // next line (or fade out) next frame
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void HandleRadioBeat(string beat)
        {
            if (script == null || !script.TryGet(beat, out var line)) return;
            while (_queue.Count >= maxQueued) _queue.Dequeue();
            _queue.Enqueue(line);
        }

        private void Begin(RadioLine line)
        {
            _current = line;
            _typed = 0f;
            _holdLeft = line.HoldSeconds;
            if (speakerText != null) speakerText.text = line.Speaker;
            if (lineText == null) return;
            // Full text laid out up front and revealed by count, so the wrap never jumps while typing.
            lineText.text = line.Text;
            lineText.maxVisibleCharacters = 0;
        }
    }
}
