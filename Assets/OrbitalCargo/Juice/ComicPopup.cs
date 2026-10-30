using TMPro;
using UnityEngine;

namespace OrbitalCargo.Juice
{
    /// <summary>
    /// World-space comic pop ("+25 ¢", "CONTRABAND!"): punches up in scale, faces the camera, rises and
    /// fades. Ported from Silly Justice <c>ComicPopup</c>, changed to TextMeshPro and to pooling — it is
    /// re-armed by <see cref="Play"/> and handed back by <see cref="JuicePresenter"/> after
    /// <see cref="Lifetime"/> instead of destroying itself. Runs on scaled time so it holds during pause.
    /// </summary>
    [AddComponentMenu("OrbitalCargo/Juice/Comic Popup")]
    public sealed class ComicPopup : MonoBehaviour
    {
        [SerializeField] private TMP_Text text;
        [Min(0.1f)] [SerializeField] private float lifetime = 1.1f;
        [Min(0f)] [SerializeField] private float startScale = 0.2f;
        [Min(0f)] [SerializeField] private float popScale = 1f;
        [Tooltip("Metres per second it rises.")]
        [Min(0f)] [SerializeField] private float rise = 0.9f;

        private float _t;
        private Camera _cam;
        private Color _baseColor;

        public float Lifetime => lifetime;

        public void Play(string message, Color color, Vector3 worldPosition)
        {
            if (_cam == null) _cam = Camera.main;
            transform.position = worldPosition;
            transform.localScale = Vector3.one * startScale;
            _t = 0f;
            _baseColor = color;
            if (text == null) return;
            text.text = message;
            text.color = color;
        }

        private void Update()
        {
            if (_t >= lifetime) return;

            _t += Time.deltaTime;
            float n = Mathf.Clamp01(_t / lifetime);

            // Punch: quick overshoot to popScale.
            float s = Mathf.Lerp(startScale, popScale, Mathf.Sin(Mathf.Clamp01(n * 2.5f) * Mathf.PI * 0.5f));
            transform.localScale = Vector3.one * s;
            transform.position += Vector3.up * (rise * Time.deltaTime);

            if (_cam != null)
                transform.rotation = Quaternion.LookRotation(transform.position - _cam.transform.position);

            if (text == null) return;
            var c = _baseColor;
            c.a = 1f - Mathf.Clamp01((n - 0.5f) / 0.5f);
            text.color = c;
        }
    }
}
