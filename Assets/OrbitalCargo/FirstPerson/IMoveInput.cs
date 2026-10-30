using UnityEngine;

namespace OrbitalCargo.FirstPerson
{
    /// <summary>
    /// Abstraction over first-person movement input (keyboard/mouse, touch, or a replay/AI driver).
    /// The controller depends on this interface, never on the Input System directly, so input can be
    /// faked in tests or swapped without touching movement code.
    /// </summary>
    public interface IMoveInput
    {
        /// <summary>Normalized planar move axis: x = strafe, y = forward/back. Range roughly [-1, 1].</summary>
        Vector2 Move { get; }

        /// <summary>Frame look delta (mouse/finger). x = yaw, y = pitch, in device units.</summary>
        Vector2 Look { get; }

        /// <summary>True while the sprint modifier is held.</summary>
        bool SprintHeld { get; }

        /// <summary>True while the crouch modifier is held.</summary>
        bool CrouchHeld { get; }

        /// <summary>True only on the frame the jump button went down.</summary>
        bool JumpPressed { get; }
    }
}
