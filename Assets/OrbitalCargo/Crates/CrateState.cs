namespace OrbitalCargo.Crates
{
    /// <summary>Lifecycle of a crate. Declared complete up front so later slices never reshuffle it.</summary>
    public enum CrateState
    {
        OnConveyor, // riding or queued on the belt — pickable
        Pickable,   // resting somewhere pickable off the belt (e.g. scanner booth output)
        Carried,    // in the player's hands
        Slotted,    // sitting in a bay slot
        Scanning,   // inside the scanner booth
        Thrown,     // airborne toward the chute
        Consumed,   // launched or chuted; about to return to the pool
    }
}
