using UnityEngine;
using UnityEngine.UI;

namespace OrbitalCargo.Crates
{
    /// <summary>
    /// The ring on a cryo crate's lid (GDD: "ring timer on the crate"): a radial fill that drains and
    /// shifts cyan → amber → red, then pulses red once expired. Purely visual; <see cref="CryoTimer"/> drives it.
    /// </summary>
    [AddComponentMenu("OrbitalCargo/Crates/Cryo Ring View")]
    public sealed class CryoRingView : MonoBehaviour
    {
        [Tooltip("Root of the ring canvas (toggled on for cryo crates only).")]
        [SerializeField] private GameObject root;
        [Tooltip("Radial-filled ring image.")]
        [SerializeField] private Image fill;

        [Header("Colours")]
        [SerializeField] private Color fullColor = new Color(0.2f, 0.9f, 1f);
        [SerializeField] private Color warnColor = new Color(1f, 0.7f, 0.1f);
        [SerializeField] private Color dangerColor = new Color(1f, 0.15f, 0.1f);
        [Tooltip("Below this fraction the ring turns amber.")]
        [Range(0f, 1f)] [SerializeField] private float warnAt = 0.5f;
        [Tooltip("Below this fraction the ring turns red.")]
        [Range(0f, 1f)] [SerializeField] private float dangerAt = 0.2f;
        [Tooltip("Pulses per second once expired.")]
        [Min(0f)] [SerializeField] private float expiredPulseHz = 2f;

        public void SetVisible(bool visible)
        {
            if (root != null && root.activeSelf != visible) root.SetActive(visible);
        }

        public void Show(float normalized, bool expired)
        {
            if (fill == null) return;
            if (expired)
            {
                fill.fillAmount = 1f;
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * expiredPulseHz * Mathf.PI * 2f);
                fill.color = Color.Lerp(dangerColor * 0.5f, dangerColor, pulse);
                return;
            }

            fill.fillAmount = normalized;
            fill.color = normalized <= dangerAt ? dangerColor
                       : normalized <= warnAt ? Color.Lerp(dangerColor, warnColor, Mathf.InverseLerp(dangerAt, warnAt, normalized))
                       : Color.Lerp(warnColor, fullColor, Mathf.InverseLerp(warnAt, 1f, normalized));
        }
    }
}
