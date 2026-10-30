using UnityEngine;

namespace OrbitalCargo.Config
{
    /// <summary>
    /// Something smuggled inside a suspicious crate (alien egg, weapons, ticking device). A crate with
    /// a hidden item is "bad": the scanner reveals it, chuting it after a scan pays the chute bonus and
    /// logs it to the collection, and shipping it makes the news (and a fine).
    /// </summary>
    [CreateAssetMenu(fileName = "HI_", menuName = "OrbitalCargo/Crates/Hidden Item")]
    public class HiddenItemSO : ScriptableObject
    {
        public string Id = "alien_egg";
        public string DisplayName = "Alien Egg";
        [Tooltip("Model shown inside the crate during (and after) the X-ray reveal.")]
        public GameObject Prefab;
        [Tooltip("Collection pop-up icon. Empty = a tinted dot.")]
        public Sprite Icon;
        [Tooltip("Colour of the pop-up icon dot when there's no Icon sprite.")]
        public Color IconTint = new Color(0.4f, 1f, 0.3f);
        [Tooltip("Collection set this item belongs to (GDD: Alien Wildlife, Black Market Tech...).")]
        public string CollectionSet = "Alien Wildlife";
        [Tooltip("News ticker headline when this item is shipped unscanned.")]
        [TextArea] public string NewsHeadline = "Alien egg hatches on colony!";
    }
}
