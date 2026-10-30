using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace OrbitalCargo.FirstPerson
{
    /// <summary>
    /// Full-screen drag region for camera look on touch. Place it FIRST (behind) in the Controls canvas
    /// so the joystick and buttons consume their own touches; this area then receives every other
    /// drag. Accumulates the drag delta each frame and clears it in LateUpdate, so an
    /// <see cref="IMoveInput"/> provider reads <see cref="Delta"/> during Update exactly like a mouse
    /// delta. Tracks a single finger so a second touch never jitters the look.
    ///
    /// Also distinguishes a quick TAP from a drag and raises <see cref="Tapped"/>, and exposes
    /// <see cref="IsDragging"/> so the junk-throw swipe can require that the gesture started on empty
    /// screen rather than on the joystick. Ported from Silly-Justice.
    /// </summary>
    [AddComponentMenu("OrbitalCargo/UI/Touch Look Area")]
    public sealed class TouchLookArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Tooltip("Multiplies the raw touch delta (pixels/frame) before it becomes look input.")]
        [SerializeField] private float sensitivity = 1f;

        [Header("Tap detection")]
        [Tooltip("Max seconds between press and release to still count as a tap (not a drag).")]
        [SerializeField] private float maxTapDuration = 0.25f;
        [Tooltip("Max finger travel (pixels) from press to release to still count as a tap.")]
        [SerializeField] private float maxTapMove = 20f;

        [Header("Swipe detection")]
        [Tooltip("Max seconds from press to release for a flick to count as a swipe (slower = just looking).")]
        [SerializeField] private float maxSwipeDuration = 0.35f;
        [Tooltip("Min travel for a swipe, as a fraction of screen height (so it's the same feel on any device).")]
        [Range(0.02f, 0.5f)] [SerializeField] private float minSwipeScreenFraction = 0.12f;

        private const int NoFinger = -2;

        private Vector2 _delta;               // accumulated this frame
        private int _activeFinger = NoFinger; // pointerId currently driving look
        private Vector2 _downPos;             // where the active finger pressed
        private float _downTime;              // when the active finger pressed (unscaled)

        /// <summary>Raised on a quick tap; argument is the screen position of the tap.</summary>
        public event Action<Vector2> Tapped;

        /// <summary>Raised on a quick flick; argument is the press→release delta in pixels (junk throw).</summary>
        public event Action<Vector2> Swiped;

        /// <summary>Drag delta accumulated this frame, scaled by sensitivity. x = yaw, y = pitch.</summary>
        public Vector2 Delta => _delta * sensitivity;

        /// <summary>True while a finger (or the mouse) is held on this area.</summary>
        public bool IsDragging => _activeFinger != NoFinger;

        // ── Pointer handlers ───────────────────────────────────────────────────

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_activeFinger != NoFinger) return;
            _activeFinger = eventData.pointerId;
            _downPos = eventData.position;
            _downTime = Time.unscaledTime;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _activeFinger) return;
            _delta += eventData.delta;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _activeFinger) return;
            _activeFinger = NoFinger;

            float duration = Time.unscaledTime - _downTime;
            Vector2 delta = eventData.position - _downPos;
            bool quick = duration <= maxTapDuration;
            bool still = delta.magnitude <= maxTapMove;
            if (quick && still)
                Tapped?.Invoke(eventData.position);
            else if (duration <= maxSwipeDuration && delta.magnitude >= Screen.height * minSwipeScreenFraction)
                Swiped?.Invoke(delta);
        }

        // ── Lifecycle ──────────────────────────────────────────────────────────

        // Runs after all Update() calls have read Delta this frame, then resets for the next frame.
        private void LateUpdate() => _delta = Vector2.zero;

        // Switched off with a finger still down (pause hides the controls): the EventSystem never
        // delivers that finger's pointer-up to an inactive control, so let go of it here.
        private void OnDisable()
        {
            _activeFinger = NoFinger;
            _delta = Vector2.zero;
        }
    }
}
