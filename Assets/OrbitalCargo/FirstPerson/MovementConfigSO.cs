using UnityEngine;

namespace OrbitalCargo.FirstPerson
{
    /// <summary>
    /// Data-only tuning for first-person locomotion. The motor reads these values; it never
    /// hard-codes speeds or gravity, so designers tune movement as an asset, not in code.
    /// Ported from Silly-Justice (FP_MovementConfig).
    /// </summary>
    [CreateAssetMenu(fileName = "FP_MovementConfig", menuName = "OrbitalCargo/FirstPerson/Movement Config")]
    public class MovementConfigSO : ScriptableObject
    {
        [Header("Speeds (m/s)")]
        [Tooltip("Standing/walking ground speed. This is the 'walk speed' tunable from KICKOFF.")]
        [Min(0f)] public float WalkSpeed = 4.5f;
        [Tooltip("Speed while the sprint modifier is held (keyboard only on mobile).")]
        [Min(0f)] public float SprintSpeed = 7.5f;
        [Tooltip("Speed while crouched (keyboard only on mobile).")]
        [Min(0f)] public float CrouchSpeed = 2.2f;

        [Header("Acceleration")]
        [Tooltip("How fast horizontal velocity ramps toward the target speed on the ground (higher = snappier).")]
        [Min(0.01f)] public float GroundAcceleration = 14f;
        [Tooltip("How much control you have mid-air (lower = more committed to your jump direction).")]
        [Min(0f)] public float AirAcceleration = 3f;

        [Header("Jump & Gravity")]
        [Tooltip("Peak height (m) of a jump from flat ground.")]
        [Min(0f)] public float JumpHeight = 1.2f;
        [Tooltip("Downward acceleration (m/s^2). 9.81 = earth; raise for a snappier, gamey fall.")]
        [Min(0f)] public float Gravity = 20f;
        [Tooltip("Small downward force kept while grounded so the controller stays glued to slopes/steps.")]
        [Min(0f)] public float GroundedStickForce = 2f;
        [Tooltip("Grace window (s) after leaving a ledge during which a jump still registers (coyote time).")]
        [Min(0f)] public float CoyoteTime = 0.12f;

        [Header("Stance Heights (m)")]
        [Tooltip("CharacterController height while standing.")]
        [Min(0.1f)] public float StandHeight = 1.8f;
        [Tooltip("CharacterController height while crouched.")]
        [Min(0.1f)] public float CrouchHeight = 1.0f;
        [Tooltip("How fast the body eases between stand and crouch heights.")]
        [Min(0.01f)] public float StanceTransitionSpeed = 10f;
    }
}
