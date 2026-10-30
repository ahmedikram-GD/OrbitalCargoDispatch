using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace GameKit.UI
{
    /// <summary>
    /// Full-screen dip-to-colour used to cover a state swap (e.g. gameplay → Home): fade the cover in,
    /// run <c>onCovered</c> while nothing is visible, hold briefly so the destination can build, then
    /// fade out over it.
    ///
    /// The overlay is built lazily at runtime (DontDestroyOnLoad, Screen Space – Overlay) so no scene
    /// or prefab has to host it — a HUD prefab can't hold a scene reference, but it can hold this asset.
    /// By default the cover takes <c>Camera.main.backgroundColor</c> at the moment it starts, which is the
    /// colour the active theme already paints behind the board, so the board appears to sink into its
    /// own backdrop in every theme without the kit knowing themes exist.
    /// </summary>
    [CreateAssetMenu(fileName = "ScreenFadeTransition", menuName = "GameKit/UI/Screen Fade Transition")]
    public class ScreenFadeTransitionSO : ScriptableObject
    {
        [Header("Timing (unscaled seconds)")]
        [SerializeField, Min(0f)] private float fadeInDuration = 0.22f;
        [SerializeField, Min(0f)] private float holdDuration = 0.08f;
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.32f;
        [SerializeField] private Ease fadeInEase = Ease.OutQuad;
        [SerializeField] private Ease fadeOutEase = Ease.InOutQuad;

        [Header("Cover")]
        [Tooltip("Use the main camera's clear colour at the moment the transition starts.")]
        [SerializeField] private bool useCameraBackgroundColor = true;
        [SerializeField] private Color coverColor = new Color(0.07f, 0.08f, 0.1f, 1f);
        [Tooltip("Must sit above every UI state canvas.")]
        [SerializeField] private int sortingOrder = 32000;

        // Runtime only. [NonSerialized] so play-mode state never writes back onto the asset.
        [NonSerialized] private CanvasGroup _group;
        [NonSerialized] private Image _image;
        [NonSerialized] private Sequence _sequence;

        public bool IsPlaying => _sequence != null && _sequence.IsActive();

        /// <summary>
        /// Plays the transition, invoking <paramref name="onCovered"/> once the screen is fully covered.
        /// Returns false (and does nothing) if a transition is already running, so a double-tap can't
        /// fire the swap twice.
        /// </summary>
        public bool Play(Action onCovered)
        {
            if (IsPlaying) return false;
            EnsureOverlay();

            Color c = coverColor;
            if (useCameraBackgroundColor && Camera.main != null) c = Camera.main.backgroundColor;
            c.a = 1f;
            _image.color = c;

            _group.gameObject.SetActive(true);
            _group.alpha = 0f;
            _group.blocksRaycasts = true; // swallow taps on the HUD / board for the whole transition

            _sequence = DOTween.Sequence().SetUpdate(true).SetLink(_group.gameObject)
                .Append(_group.DOFade(1f, fadeInDuration).SetEase(fadeInEase))
                .AppendCallback(() => SafeInvoke(onCovered))
                .AppendInterval(holdDuration)
                .Append(_group.DOFade(0f, fadeOutDuration).SetEase(fadeOutEase))
                .OnKill(Finish); // OnKill also runs after a normal completion
            return true;
        }

        private void Finish()
        {
            _sequence = null;
            if (_group == null) return;
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.gameObject.SetActive(false);
        }

        private static void SafeInvoke(Action action)
        {
            // An exception inside the swap must not strand an opaque, raycast-blocking cover on screen.
            try { action?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        private void EnsureOverlay()
        {
            if (_group != null) return;

            var root = new GameObject("[ScreenFadeTransition]",
                typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(CanvasGroup));
            DontDestroyOnLoad(root);
            root.layer = LayerMask.NameToLayer("UI");

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var cover = new GameObject("Cover", typeof(RectTransform), typeof(Image));
            cover.layer = root.layer;
            var rt = (RectTransform)cover.transform;
            rt.SetParent(root.transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            _image = cover.GetComponent<Image>();
            _image.raycastTarget = true;
            _group = root.GetComponent<CanvasGroup>();
            Finish();
        }
    }
}
