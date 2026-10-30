using UnityEngine;

namespace OrbitalCargo.Config
{
    /// <summary>
    /// Something smuggled inside a suspicious crate (alien egg, weapons, ticking device). A crate with
    /// a hidden item is "bad": scanning reveals it, chuting it pays a bonus and logs it to the
    /// collection. Fleshed out in Slice 5 (icon, collection set); declared now so <c>Crate</c> can
    /// carry the reference from day one.
    /// </summary>
    [CreateAssetMenu(fileName = "HI_", menuName = "OrbitalCargo/Crates/Hidden Item")]
    public class HiddenItemSO : ScriptableObject
    {
        public string Id = "alien_egg";
        public string DisplayName = "Alien Egg";
        [Tooltip("Model shown inside the crate during the X-ray reveal.")]
        public GameObject Prefab;
    }
}
