using UnityEngine;

namespace OrbitalCargo.FirstPerson
{
    /// <summary>
    /// Contract for turning look input into orientation — yaw on the body, pitch on the camera.
    /// Kept separate from the motor so aiming and locomotion evolve independently.
    /// </summary>
    public interface ILookController
    {
        /// <summary>Current camera pitch in degrees (negative = looking up, positive = down, Unity convention).</summary>
        float Pitch { get; }

        /// <summary>Apply a frame of look input. <paramref name="lookDelta"/>: x = yaw, y = pitch.</summary>
        void Look(Vector2 lookDelta);
    }
}
