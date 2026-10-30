using UnityEngine;

namespace OrbitalCargo.FirstPerson
{
    /// <summary>
    /// Data-only tuning for first-person look. The look controller reads these values so
    /// sensitivity, inversion and pitch limits are designer-tunable assets, not code.
    /// Ported from Silly-Justice (FP_LookConfig).
    /// </summary>
    [CreateAssetMenu(fileName = "FP_LookConfig", menuName = "OrbitalCargo/FirstPerson/Look Config")]
    public class LookConfigSO : ScriptableObject
    {
        [Header("Sensitivity")]
        [Tooltip("Degrees turned per unit of look delta. Mouse/finger deltas are in pixels, so keep this small.")]
        [Min(0f)] public float Sensitivity = 0.1f;
        [Tooltip("Invert vertical look (pull-down-to-look-up).")]
        public bool InvertY = false;

        [Header("Pitch Limits (deg)")]
        [Tooltip("Furthest the camera can look up (Unity pitch is negative when looking up).")]
        [Range(-89f, 0f)] public float MinPitch = -85f;
        [Tooltip("Furthest the camera can look down.")]
        [Range(0f, 89f)] public float MaxPitch = 85f;

        [Header("Yaw Limits (deg)")]
        [Tooltip("ON: horizontal look is clamped to ±Yaw Range around the facing the player spawned " +
                 "with. OFF: free 360° turning — the default for a walk-around room.")]
        public bool LimitYaw = false;
        [Tooltip("How far (deg) the player can turn left/right from the starting facing when Limit Yaw is on.")]
        [Range(1f, 180f)] public float YawRange = 60f;

        [Header("Smoothing")]
        [Tooltip("0 = raw/instant look. Higher = smoother but laggier. Keep low for responsive aim.")]
        [Range(0f, 30f)] public float Smoothing = 0f;
    }
}
