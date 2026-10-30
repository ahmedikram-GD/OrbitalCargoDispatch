using UnityEngine;

namespace OrbitalCargo.Interaction
{
    /// <summary>
    /// A station the player can act on with the context button: a bay (Place), a launch button
    /// (Launch), the scanner (Scan), the chute (Drop). Found by <c>GetComponentInParent</c> from the
    /// collider the centre ray hits, so any child collider of the station counts.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Button label for the current situation; empty string = nothing to offer (button hidden).</summary>
        string Prompt(CrateCarrier carrier);

        /// <summary>Whether the button is enabled (a non-empty prompt may still be disabled, e.g. "Bay full").</summary>
        bool CanInteract(CrateCarrier carrier);

        void Interact(CrateCarrier carrier, RaycastHit hit);
    }

    /// <summary>Optional: an interactable that previews the result while aimed at (the bay's ghost crate).</summary>
    public interface IInteractPreview
    {
        void ShowPreview(CrateCarrier carrier, RaycastHit hit);
        void HidePreview();
    }
}
