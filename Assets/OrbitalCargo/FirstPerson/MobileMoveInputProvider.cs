using UnityEngine;
using UnityEngine.InputSystem;

namespace OrbitalCargo.FirstPerson
{
    /// <summary>
    /// Mobile <see cref="IMoveInput"/>: move from the on-screen <see cref="VirtualJoystick"/>, look from
    /// the <see cref="TouchLookArea"/> drag. Hybrid in the Editor — when the touch controls are idle it
    /// also reads keyboard WASD, and mouse look while the <b>right mouse button</b> is held (so moving
    /// the mouse to the HUD doesn't spin the camera). Ships to device unchanged because the fallback is
    /// off outside the Editor by default. Ported from Silly-Justice.
    /// </summary>
    [AddComponentMenu("OrbitalCargo/First Person/Mobile Move Input Provider")]
    public sealed class MobileMoveInputProvider : MonoBehaviour, IMoveInput
    {
        [Header("Touch controls")]
        [Tooltip("The Controls canvas joystick (bottom-left). Left empty = keyboard only.")]
        [SerializeField] private VirtualJoystick joystick;
        [Tooltip("The Controls canvas full-screen look area. Left empty = mouse only.")]
        [SerializeField] private TouchLookArea lookArea;

        [Header("Editor fallback")]
        [Tooltip("Also read keyboard (WASD) and mouse look (right button held) so the game is testable in the " +
                 "Editor. Defaults ON in the Editor, OFF in builds.")]
        [SerializeField] private bool allowKeyboardMouse =
#if UNITY_EDITOR
            true;
#else
            false;
#endif

        public Vector2 Move
        {
            get
            {
                Vector2 m = joystick != null ? joystick.Value : Vector2.zero;
                if (allowKeyboardMouse && m.sqrMagnitude < 0.0001f)
                    m = FirstPersonInputProvider.KeyboardMove();
                return m.sqrMagnitude > 1f ? m.normalized : m;
            }
        }

        public Vector2 Look
        {
            get
            {
                Vector2 l = lookArea != null ? lookArea.Delta : Vector2.zero;
                // Only add raw mouse delta while right-dragging and no finger/left-drag owns the look area,
                // otherwise an Editor mouse drag on the look area would be counted twice.
                bool lookAreaIdle = lookArea == null || !lookArea.IsDragging;
                if (allowKeyboardMouse && lookAreaIdle && Mouse.current != null && Mouse.current.rightButton.isPressed)
                    l += Mouse.current.delta.ReadValue();
                return l;
            }
        }

        public bool SprintHeld => allowKeyboardMouse && Keyboard.current != null
            && Keyboard.current.leftShiftKey.isPressed;

        public bool CrouchHeld => allowKeyboardMouse && Keyboard.current != null
            && (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.cKey.isPressed);

        // Jump is keyboard-only and uses Space; KICKOFF reserves Space for the junk throw in the Editor,
        // so jump is bound to J to keep both testable.
        public bool JumpPressed => allowKeyboardMouse && Keyboard.current != null
            && Keyboard.current.jKey.wasPressedThisFrame;
    }
}
