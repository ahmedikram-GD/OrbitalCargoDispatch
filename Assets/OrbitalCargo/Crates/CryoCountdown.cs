namespace OrbitalCargo.Crates
{
    /// <summary>
    /// Pure cryo ring clock: counts down only while running, reports progress, and expires exactly
    /// once. The <c>CryoTimer</c> component decides when it runs (on the belt / in hand) and drives the ring.
    /// </summary>
    public sealed class CryoCountdown
    {
        public float Total { get; private set; }
        public float Remaining { get; private set; }
        public float Normalized => Total > 0f ? Remaining / Total : 0f;
        public bool Expired { get; private set; }

        public void Reset(float seconds)
        {
            Total = seconds > 0f ? seconds : 0f;
            Remaining = Total;
            Expired = false;
        }

        /// <summary>Advance by <paramref name="dt"/>. Returns true only on the tick it expires.</summary>
        public bool Tick(float dt, bool running)
        {
            if (!running || Expired || Total <= 0f) return false;
            Remaining -= dt;
            if (Remaining > 0f) return false;
            Remaining = 0f;
            Expired = true;
            return true;
        }
    }
}
