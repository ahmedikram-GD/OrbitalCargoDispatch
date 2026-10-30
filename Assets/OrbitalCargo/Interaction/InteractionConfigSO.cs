using UnityEngine;

namespace OrbitalCargo.Interaction
{
    /// <summary>
    /// Tuning for aiming at, carrying and placing crates: reach, how a crate flies to the hand and to
    /// a slot, how big it looks while carried, and how many fit in the hands. Ported from
    /// Silly-Justice (FP_InteractionConfig) with the throw/animation fields replaced by carry fields.
    /// </summary>
    [CreateAssetMenu(fileName = "FP_InteractionConfig", menuName = "OrbitalCargo/Interaction/Interaction Config")]
    public class InteractionConfigSO : ScriptableObject
    {
        [Header("Aim")]
        [Tooltip("How far ahead (m) the centre-screen ray reaches from the camera.")]
        [Min(0f)] public float PickRange = 3f;
        [Tooltip("Layers the ray can hit. The first thing hit decides the target, so walls block reach.")]
        public LayerMask PickMask = ~0;

        [Header("Carry")]
        [Tooltip("Crates the hands hold at once. Upgrades (grav-sled) raise it at runtime.")]
        [Min(1)] public int CarryCapacity = 1;
        [Tooltip("Scale of a carried crate relative to its full size, so it doesn't fill the view.")]
        [Range(0.2f, 1f)] public float CarriedScale = 0.55f;
        [Tooltip("Vertical spacing (m, in hold-point space) between stacked carried crates.")]
        [Min(0f)] public float CarryStackSpacing = 0.46f;

        [Header("Motion")]
        [Tooltip("Seconds for a crate to fly into the hand.")]
        [Min(0.01f)] public float FlyToHandDuration = 0.18f;
        [Tooltip("Seconds for a crate to fly from the hand onto a slot.")]
        [Min(0.01f)] public float SnapDuration = 0.15f;
        [Tooltip("Height (m) of the little arc crates follow when they move.")]
        [Min(0f)] public float FlyArcHeight = 0.15f;

        [Header("Prompts")]
        public string PickUpPrompt = "Pick up";
        public string PlacePrompt = "Place";
        public string BayFullPrompt = "Bay full";
        public string HandsFullPrompt = "Hands full";
    }
}
