using UnityEngine;
using UnityEngine.EventSystems;

namespace OrbitalCargo.FirstPerson
{
    /// <summary>
    /// On-screen virtual joystick (uGUI). Reports a normalized <see cref="Value"/> in [-1, 1] per
    /// axis, matching the keyboard move axis so it plugs straight into <see cref="IMoveInput"/> via
    /// <see cref="MobileMoveInputProvider"/>. EventSystem-driven, so it coexists with the full-screen
    /// <see cref="TouchLookArea"/> under multitouch — each control owns the finger that pressed it.
    /// Ported from Silly-Justice with the projectile-steering modes removed (classic stick only).
    /// </summary>
    [AddComponentMenu("OrbitalCargo/UI/Virtual Joystick")]
    public sealed class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("References")]
        [Tooltip("The circular area the handle moves within. Its half-size defines the stick radius.")]
        [SerializeField] private RectTransform background;
        [Tooltip("The knob that follows the finger inside the background.")]
        [SerializeField] private RectTransform handle;

        [Header("Tuning")]
        [Tooltip("Fraction of the background radius the handle is allowed to travel (visual only).")]
        [SerializeField, Range(0.1f, 1f)] private float handleRange = 1f;
        [Tooltip("Inputs below this magnitude read as zero (stick dead-zone).")]
        [SerializeField, Range(0f, 0.5f)] private float deadZone = 0.05f;
        [Tooltip("Floating stick: the background recenters under the finger on touch (GDD: floating thumbstick).")]
        [SerializeField] private bool floating = true;
        [Tooltip("Hide the stick visual until a finger touches this control, then show it under the finger. " +
                 "The touch area is this object's own (invisible) graphic.")]
        [SerializeField] private bool hideUntilTouched = false;

        private Camera _uiCamera;
        private Vector2 _input;            // normalized -1..1
        private Vector2 _homeAnchoredPos;  // background rest position (for floating restore)

        /// <summary>Normalized stick input: x = strafe, y = forward/back, magnitude clamped to 1.</summary>
        public Vector2 Value => _input;

        /// <summary>True while a finger is on the stick — even at dead-centre.</summary>
        public bool IsPressed { get; private set; }

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                _uiCamera = canvas.worldCamera;
            if (background != null) _homeAnchoredPos = background.anchoredPosition;
            if (hideUntilTouched) SetVisualVisible(false);
        }

        // Switched off with a finger still on the stick (pause hides the controls): its pointer-up
        // never arrives, so drop the press here or the stick keeps reporting the old push.
        private void OnDisable()
        {
            IsPressed = false;
            _input = Vector2.zero;
            if (handle != null) handle.anchoredPosition = Vector2.zero;
            if (floating && background != null) background.anchoredPosition = _homeAnchoredPos;
        }

        // ── Pointer handlers ───────────────────────────────────────────────────

        public void OnPointerDown(PointerEventData eventData)
        {
            IsPressed = true;
            if (hideUntilTouched) SetVisualVisible(true);
            // Place the stick by WORLD position, not anchoredPosition: a parent-local point is measured
            // from the parent's pivot while anchoredPosition is measured from the background's anchors, so
            // the SJ original put the stick half a zone away from the finger (instant full deflection)
            // whenever its anchors weren't centred. World space is anchor-independent.
            if (floating && background != null && background.parent is RectTransform parent
                && RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, eventData.position, _uiCamera, out var worldPoint))
            {
                background.position = worldPoint;
            }
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (background == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(background, eventData.position, _uiCamera, out var local))
                return;

            Vector2 size = background.rect.size;
            float radius = Mathf.Min(size.x, size.y) * 0.5f;
            if (radius <= 0f) return;

            Vector2 normalized = local / radius;                 // 1 at the edge
            if (normalized.magnitude > 1f) normalized = normalized.normalized;

            _input = normalized.magnitude < deadZone ? Vector2.zero : normalized;

            if (handle != null)
                handle.anchoredPosition = _input * radius * handleRange;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            IsPressed = false;
            _input = Vector2.zero;
            if (handle != null) handle.anchoredPosition = Vector2.zero;
            if (floating && background != null) background.anchoredPosition = _homeAnchoredPos;
            if (hideUntilTouched) SetVisualVisible(false);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        // Show/hide the stick artwork without disabling THIS component, so the invisible touch area
        // keeps receiving pointer events.
        private void SetVisualVisible(bool visible)
        {
            if (background != null && background.gameObject != gameObject)
                background.gameObject.SetActive(visible);
        }
    }
}
