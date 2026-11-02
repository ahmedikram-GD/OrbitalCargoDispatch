namespace OrbitalCargo.Interaction
{
    /// <summary>
    /// Optional companion to <see cref="IInteractable"/>: the interaction is allowed but probably a
    /// mistake (a crate aimed at the wrong bay). The context button turns red; nothing is blocked.
    /// </summary>
    public interface IInteractWarning
    {
        bool IsWarning(CrateCarrier carrier);
    }
}
