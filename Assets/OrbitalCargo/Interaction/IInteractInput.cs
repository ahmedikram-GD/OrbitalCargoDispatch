namespace OrbitalCargo.Interaction
{
    /// <summary>
    /// Interact/throw intent. Keyboard keys and the on-screen context button feed the same contract,
    /// so the interactor never knows where a request came from. Consume model (latched until read)
    /// so a UI click dispatched at any point in the frame is never dropped. Ported from Silly-Justice.
    /// </summary>
    public interface IInteractInput
    {
        /// <summary>True once per interact request (pick up / place / scan / launch), clearing it.</summary>
        bool ConsumeInteract();

        /// <summary>True once per throw request (junk swipe or Space), clearing it.</summary>
        bool ConsumeThrow();
    }
}
