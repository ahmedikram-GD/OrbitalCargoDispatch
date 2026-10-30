namespace OrbitalCargo.Economy
{
    /// <summary>
    /// Per-shift tallies for the summary screen. A plain bag owned by <c>ShiftController</c>, filled
    /// from the same events the juice listens to (launches, collected items, news fines) so no
    /// gameplay component has to know the summary exists.
    /// </summary>
    public sealed class ShiftStats
    {
        public int CreditsEarned;
        public int Launches;
        public int CratesShipped;
        public int ItemsFound;
        public int Fines;

        public void Reset()
        {
            CreditsEarned = 0;
            Launches = 0;
            CratesShipped = 0;
            ItemsFound = 0;
            Fines = 0;
        }
    }
}
