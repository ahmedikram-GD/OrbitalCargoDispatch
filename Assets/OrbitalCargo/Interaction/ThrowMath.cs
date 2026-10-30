using UnityEngine;

namespace OrbitalCargo.Interaction
{
    /// <summary>Ballistics for the assisted junk throw.</summary>
    public static class ThrowMath
    {
        /// <summary>
        /// Launch velocity that carries a body from <paramref name="start"/> to <paramref name="target"/>
        /// in exactly <paramref name="flightTime"/> seconds under <paramref name="gravity"/> (no drag).
        /// </summary>
        public static Vector3 VelocityToHit(Vector3 start, Vector3 target, float flightTime, Vector3 gravity)
        {
            // p(t) = p0 + v·t + ½·g·t²  →  v = (p - p0) / t - ½·g·t
            float t = Mathf.Max(flightTime, 1e-3f);
            return (target - start) / t - 0.5f * t * gravity;
        }
    }
}
