using UnityEngine;
using UnityEngine.InputSystem;

namespace OrbitalCargo.FirstPerson
{
    /// <summary>
    /// Desktop-only <see cref="IMoveInput"/> backed by the Input System's keyboard and mouse devices
    /// (WASD + mouse-look, Shift sprint, Ctrl/C crouch, Space jump). All device access is null-guarded
    /// so a missing device never throws. The shipped player uses <see cref="MobileMoveInputProvider"/>
    /// (which includes this behaviour as its Editor fallback); keep this for a pure-desktop test rig.
    /// Ported from Silly-Justice.
    /// </summary>
    [AddComponentMenu("OrbitalCargo/First Person/First Person Input Provider (Desktop)")]
    public sealed class FirstPersonInputProvider : MonoBehaviour, IMoveInput
    {
        public Vector2 Move => KeyboardMove();

        public Vector2 Look => Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;

        public bool SprintHeld => Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;

        public bool CrouchHeld => Keyboard.current != null
            && (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.cKey.isPressed);

        public bool JumpPressed => Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;

        /// <summary>WASD / arrow keys as a clamped planar axis (diagonals aren't faster than cardinals).</summary>
        public static Vector2 KeyboardMove()
        {
            var kb = Keyboard.current;
            if (kb == null) return Vector2.zero;

            float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f)
                    - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
            float y = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f)
                    - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);

            var move = new Vector2(x, y);
            return move.sqrMagnitude > 1f ? move.normalized : move;
        }
    }
}
