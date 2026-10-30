using UnityEngine;

namespace OrbitalCargo.FirstPerson
{
    /// <summary>
    /// Data-only tuning for the first-person camera bob/breathing. <see cref="CameraBobController"/>
    /// reads these values so idle breathing and locomotion bob are designer-tunable assets.
    /// All amplitudes are in metres of local offset; frequencies are in cycles per second (Hz).
    /// Ported from Silly-Justice (FP_CameraBobConfig).
    /// </summary>
    [CreateAssetMenu(fileName = "FP_CameraBobConfig", menuName = "OrbitalCargo/FirstPerson/Camera Bob Config")]
    public class CameraBobConfigSO : ScriptableObject
    {
        [Header("Idle Breathing")]
        [Tooltip("Vertical rise/fall of the camera while standing still (metres). Keep tiny.")]
        [Min(0f)] public float IdleVerticalAmplitude = 0.012f;
        [Tooltip("Sideways sway while standing still (metres). Keep tiny.")]
        [Min(0f)] public float IdleHorizontalAmplitude = 0.006f;
        [Tooltip("Breaths per second. ~0.2-0.3 reads as calm breathing.")]
        [Min(0f)] public float IdleFrequency = 0.25f;

        [Header("Walk Bob")]
        [Tooltip("Planar speed (m/s) above which the camera is considered moving rather than idle.")]
        [Min(0f)] public float MoveSpeedThreshold = 0.3f;
        [Tooltip("Vertical bob amplitude at walking pace (metres). Minimal by design.")]
        [Min(0f)] public float WalkVerticalAmplitude = 0.022f;
        [Tooltip("Horizontal bob amplitude at walking pace (metres).")]
        [Min(0f)] public float WalkHorizontalAmplitude = 0.014f;
        [Tooltip("Bob cycles per second at walking pace.")]
        [Min(0f)] public float WalkFrequency = 1.6f;

        [Header("Run Bob")]
        [Tooltip("Planar speed (m/s) at which run bob values are fully reached; blended in from the walk threshold.")]
        [Min(0f)] public float RunSpeedThreshold = 5f;
        [Tooltip("Vertical bob amplitude at running pace (metres).")]
        [Min(0f)] public float RunVerticalAmplitude = 0.03f;
        [Tooltip("Horizontal bob amplitude at running pace (metres).")]
        [Min(0f)] public float RunHorizontalAmplitude = 0.02f;
        [Tooltip("Bob cycles per second at running pace.")]
        [Min(0f)] public float RunFrequency = 2.2f;

        [Header("Blending")]
        [Tooltip("Seconds to smooth transitions between idle and move states. Higher = softer, laggier.")]
        [Min(0f)] public float TransitionSmoothTime = 0.15f;
    }
}
