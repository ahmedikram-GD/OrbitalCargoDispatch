namespace OrbitalCargo.Interaction
{
    /// <summary>
    /// Something that can be outlined while it's the interactor's focus. The interactor depends only
    /// on this contract, never on the outline technique. Ported from Silly-Justice.
    /// </summary>
    public interface IHighlightable
    {
        /// <summary>Turn the focus highlight on or off. Idempotent.</summary>
        void SetHighlighted(bool on);
    }
}
