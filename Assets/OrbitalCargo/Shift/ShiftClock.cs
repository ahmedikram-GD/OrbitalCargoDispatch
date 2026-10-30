namespace OrbitalCargo.Shift
{
    /// <summary>
    /// Pure shift timer. <c>ShiftController</c> ticks it with scaled delta time, so pausing
    /// (timeScale 0) freezes the shift for free. Ends exactly once: <see cref="Tick"/> returns true
    /// on the frame the shift runs out and never again until <see cref="Reset"/>.
    /// </summary>
    public sealed class ShiftClock
    {
        public float Length { get; private set; }
        public float Elapsed { get; private set; }
        public float Remaining => Length - Elapsed;
        public bool IsOver { get; private set; }

        public ShiftClock(float lengthSeconds)
        {
            Reset(lengthSeconds);
        }

        public void Reset(float lengthSeconds)
        {
            Length = lengthSeconds > 0f ? lengthSeconds : 0f;
            Elapsed = 0f;
            IsOver = false;
        }

        /// <summary>Advance by <paramref name="dt"/>. Returns true only on the tick the shift ends.</summary>
        public bool Tick(float dt)
        {
            if (IsOver) return false;
            Elapsed += dt;
            if (Elapsed < Length) return false;
            Elapsed = Length;
            IsOver = true;
            return true;
        }
    }
}
