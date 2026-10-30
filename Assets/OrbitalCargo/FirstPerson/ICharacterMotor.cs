using UnityEngine;

namespace OrbitalCargo.FirstPerson
{
    /// <summary>
    /// Contract for whatever physically moves the character body (CharacterController today).
    /// The orchestrator drives this interface, so the underlying motor can be swapped without
    /// changing the controller.
    /// </summary>
    public interface ICharacterMotor
    {
        /// <summary>True when the body is resting on ground this frame.</summary>
        bool IsGrounded { get; }

        /// <summary>Current world-space velocity of the body (including vertical).</summary>
        Vector3 Velocity { get; }

        /// <summary>
        /// Multiplier applied to the target walking speed (1 = normal). Carrying a volatile crate
        /// lowers it; the carrier owns the value and restores it on release.
        /// </summary>
        float SpeedMultiplier { get; set; }

        /// <summary>
        /// Advance the body for one step. <paramref name="moveInput"/> is planar (x = strafe,
        /// y = forward) in the body's local space; the motor applies speed, acceleration and gravity.
        /// </summary>
        void Move(Vector2 moveInput, bool sprint, bool crouch);

        /// <summary>Request a jump; the motor decides whether it is currently allowed (e.g. grounded).</summary>
        void Jump();
    }
}
