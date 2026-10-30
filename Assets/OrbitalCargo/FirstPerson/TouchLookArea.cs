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

        private const int NoFinger = -2;

        private Vector2 _delta;               // accumulated this frame
        private int _activeFinger = NoFinger; // pointerId currently driving look
        private Vector2 _downPos;             // where the active finger pressed
        private float _downTime;              // when the active finger pressed (unscaled)

        /// <summary>Raised on a quick tap; argument is the screen position of the tap.</summary>
        public event Action<Vector2> Tapped;

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

            bool quick = Time.unscaledTime - _downTime <= maxTapDuration;
            bool still = Vector2.Distance(eventData.position, _downPos) <= maxTapMove;
            if (quick && still) Tapped?.Invoke(eventData.position);
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
