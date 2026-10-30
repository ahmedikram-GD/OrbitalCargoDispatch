using UnityEngine;

namespace OrbitalCargo.Juice
{
    /// <summary>
    /// How one camera shake feels: duration, position wobble, tilt, frequency and decay. One asset per
    /// feel (Shake_Launch...). <see cref="CameraShaker"/> plays it. Ported from Silly-Justice.
    /// </summary>
    [CreateAssetMenu(fileName = "Shake_", menuName = "OrbitalCargo/Juice/Camera Shake Profile")]
    public class CameraShakeProfileSO : ScriptableObject
    {
        [Tooltip("Real seconds the shake lasts (unscaled).")]
        [Min(0f)] [SerializeField] private float duration = 0.3f;

        [Tooltip("Camera-local wobble in metres: X = right, Y = up, Z = forward. 0.05 is a bump, 0.3 a quake.")]
        [SerializeField] private Vector3 positionAmplitude = new Vector3(0.12f, 0.12f, 0f);

        [Tooltip("Tilt in degrees: X = pitch, Y = yaw, Z = roll.")]
        [SerializeField] private Vector3 rotationAmplitude = new Vector3(1.5f, 1.5f, 2f);

        [Tooltip("How fast it wobbles. ↑ = buzzy, ↓ = heavy sway.")]
        [Min(0f)] [SerializeField] private float frequency = 25f;

        [Tooltip("Strength over the shake's life (x: 0 = start, 1 = end).")]
        [SerializeField] private AnimationCurve decay = new AnimationCurve(
            new Keyframe(0f, 1f, -2f, -2f),
            new Keyframe(1f, 0f, 0f, 0f));

        public float Duration => duration;

        /// <summary>Offset <paramref name="elapsed"/> seconds in (camera-local). False once over.</summary>
        public bool Sample(float elapsed, float seed, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (duration <= 0f || elapsed < 0f || elapsed >= duration) return false;

            float strength = decay != null && decay.length > 0
                ? Mathf.Clamp01(decay.Evaluate(elapsed / duration))
                : 1f - elapsed / duration;
            float t = elapsed * frequency;

            position = Vector3.Scale(positionAmplitude, Noise3(t, seed)) * strength;
            rotation = Quaternion.Euler(Vector3.Scale(rotationAmplitude, Noise3(t, seed + 50f)) * strength);
            return true;
        }

        /// <summary>Editor/factory setter so default profiles can be authored from code.</summary>
        public void Configure(float seconds, Vector3 position, Vector3 rotation, float hz)
        {
            duration = seconds;
            positionAmplitude = position;
            rotationAmplitude = rotation;
            frequency = hz;
        }

        private static Vector3 Noise3(float t, float seed) =>
            new Vector3(Centered(t, seed), Centered(t, seed + 17.3f), Centered(t, seed + 31.7f));

        private static float Centered(float t, float lane) =>
            Mathf.Clamp(Mathf.PerlinNoise(t, lane) * 2f - 1f, -1f, 1f);
    }
}
