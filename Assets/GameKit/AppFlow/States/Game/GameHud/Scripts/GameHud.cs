using System.Collections;
using GameKit.Architecture.EditorTools;
using DG.Tweening;
using GameKit.Architecture.Events;
using Sirenix.OdinInspector;
using GameKit.UI;
using UnityEngine;
using GameKit.AppFlow;
using GameKit.Architecture.Variables;

namespace GameKit.AppFlow
{
    public class GameHud : MonoBehaviour, IShowable
    {
        [ColorFoldoutGroup("BaseHud", 1, 1, 4)]
        [SerializeField] private UiConfig HudBarsConfig;

        [ColorFoldoutGroup("BaseHud", 1, 1, 4)]
        [SerializeField] private RectTransform Header;

        [ColorFoldoutGroup("BaseHud", 1, 1, 4)]
        [SerializeField] private RectTransform Footer;

        /// <summary>Runtime access for subclasses that need to adjust Footer before caching.</summary>
        protected RectTransform FooterRect => Footer;

        [ColorFoldoutGroup("BaseHud", 1, 1, 4)]
        [SerializeField] private RectTransform Middle;

        [ColorFoldoutGroup("BaseHud", 1, 1, 4)]
        [Tooltip("Fire this SO event from any state/tutorial that wants to slide bars back in.")]
        [SerializeField] private GameEvent _showBarsEvent;

        [ColorFoldoutGroup("BaseHud", 1, 1, 4)]
        [Tooltip("Fire this SO event from any state/tutorial that wants to slide bars out.")]
        [SerializeField] private GameEvent _hideBarsEvent;
        
        [ColorFoldoutGroup("BaseHud", 1, 1, 4)]
        [Tooltip("Fire this SO event from instant slide out of bars.")]
        [SerializeField] private GameEvent _snapBarOffEvent;

        // ── Slide-in (optional) ──────────────────────────────────────────────
        // When on, ShowBars also slides each bar in from its own screen edge — header down from
        // above, footer up from below — each with its own ease, on top of the alpha fade. Off by
        // default so kit HUDs that only fade are unchanged.

        [ColorFoldoutGroup("BarSlideIn", 1, 1, 4)]
        [Tooltip("Slide the header/footer in from off-screen on ShowBars (in addition to the fade).")]
        [SerializeField] private bool _slideBarsIn = false;

        [ColorFoldoutGroup("BarSlideIn", 1, 1, 4)]
        [Tooltip("Extra distance (canvas units) beyond each bar's own height, so shadows/overhang start fully off-screen.")]
        [SerializeField] private float _slideExtraDistance = 40f;

        [ColorFoldoutGroup("BarSlideIn", 1, 1, 4)]
        [SerializeField] private float _headerSlideDuration = 0.55f;
        [ColorFoldoutGroup("BarSlideIn", 1, 1, 4)]
        [Tooltip("Header drops in and settles with a small overshoot.")]
        [SerializeField] private Ease _headerSlideEase = Ease.OutBack;
        [ColorFoldoutGroup("BarSlideIn", 1, 1, 4)]
        [SerializeField] private float _headerSlideDelay = 0f;

        [ColorFoldoutGroup("BarSlideIn", 1, 1, 4)]
        [SerializeField] private float _footerSlideDuration = 0.5f;
        [ColorFoldoutGroup("BarSlideIn", 1, 1, 4)]
        [Tooltip("Footer rises and glides to a stop — no overshoot, so it reads differently from the header.")]
        [SerializeField] private Ease _footerSlideEase = Ease.OutQuint;
        [ColorFoldoutGroup("BarSlideIn", 1, 1, 4)]
        [Tooltip("Footer follows the header a beat later.")]
        [SerializeField] private float _footerSlideDelay = 0.08f;

        private Tween _headerSlide;
        private Tween _footerSlide;

        // Bars fade in/out in place — CanvasGroups drive the alpha and block
        // raycasts while hidden so transparent bars never swallow taps.
        private CanvasGroup _headerCanvasGroup;
        private CanvasGroup _footerCanvasGroup;

        protected Canvas _canvas;
        protected CanvasGroup _canvasGroup;

        [SerializeField] private Int currentStateSortingOrder;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        protected virtual void Awake()
        {
            if (_canvasGroup == null)
            {
                _canvas = GetComponent<Canvas>();
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvas.worldCamera == null)
                    _canvas.worldCamera = Camera.main;
                if (_canvasGroup == null)
                    _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            _headerCanvasGroup = EnsureCanvasGroup(Header);
            _footerCanvasGroup = EnsureCanvasGroup(Footer);
        }

        private static CanvasGroup EnsureCanvasGroup(RectTransform rect)
        {
            if (rect == null) return null;
            var cg = rect.GetComponent<CanvasGroup>();
            return cg != null ? cg : rect.gameObject.AddComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            _showBarsEvent?.Subscribe(ShowBars);
            _hideBarsEvent?.Subscribe(HideBars);
            _snapBarOffEvent?.Subscribe(SnapSectionsOffScreen);
        }

        private void OnDisable()
        {
            _showBarsEvent?.UnSubscribe(ShowBars);
            _hideBarsEvent?.UnSubscribe(HideBars);
            _snapBarOffEvent?.UnSubscribe(SnapSectionsOffScreen);

            // Kill any in-flight bar fades so a DOFade never runs on a released/destroyed CanvasGroup
            // (e.g. when the game is torn down instantly on the way Home without the slide-out Hide()).
            if (_headerCanvasGroup != null) _headerCanvasGroup.DOKill();
            if (_footerCanvasGroup != null) _footerCanvasGroup.DOKill();
            if (_canvasGroup != null) _canvasGroup.DOKill();
            LandSlides();
        }

        // ── IShowable ─────────────────────────────────────────────────────────

        /// <summary>Called by GameState.Enter — slides bars in.</summary>
        public virtual void Show(bool playDefaultAnimation = false, bool makeStateinteractable = true)
        {
            ResetToStretch(GetComponent<RectTransform>());

            if (currentStateSortingOrder != null)
                _canvas.sortingOrder = currentStateSortingOrder.GetValue();

            _canvasGroup.interactable   = true;
            _canvasGroup.blocksRaycasts = true;

            ShowBars();
        }

        /// <summary>Called by GameState.Exit — slides bars out then yields.</summary>
        public virtual IEnumerator Hide(bool playDefaultAnimation = false)
        {
            _canvasGroup.interactable   = false;
            _canvasGroup.blocksRaycasts = false;

            HideBars();

            yield return new WaitForSeconds(HudBarsConfig != null ? HudBarsConfig.easeOutDuration : 0f);
        }

        /// <summary>
        /// Called by GameState.Pause — disables interaction only.
        /// Bars stay on-screen; use _hideBarsEvent if you need them hidden.
        /// </summary>
        public virtual void Pause()
        {
            _canvasGroup.interactable   = false;
            _canvasGroup.blocksRaycasts = false;
        }

        /// <summary>
        /// Called by GameState.Resume — re-enables interaction only.
        /// Bars stay where they are; use _showBarsEvent if you need them shown.
        /// </summary>
        public virtual void Resume()
        {
            _canvasGroup.interactable   = true;
            _canvasGroup.blocksRaycasts = true;
        }

        // ── Bar animations ────────────────────────────────────────────────────

        /// <summary>
        /// Fades header and footer in (alpha 0 → 1) in place. Replaces the previous
        /// slide-in motion — bars stay at their original anchored position. The
        /// starting alpha is always forced to 0 so the fade is visible even if the
        /// bars were already shown (mirrors the old SlideIn which always snapped
        /// off-screen before animating in).
        /// Called automatically on Show(), and by _showBarsEvent subscribers
        /// (e.g. after LevelComplete, after tutorials dismiss).
        /// </summary>
        public virtual void ShowBars()
        {
            float dur  = HudBarsConfig != null ? HudBarsConfig.easeInDuration : 0.25f;
            Ease  ease = HudBarsConfig != null ? HudBarsConfig.easeIn : Ease.OutCubic;
            FadeBar(_headerCanvasGroup, fromAlpha: 0f, toAlpha: 1f, dur, ease, interactive: true);
            FadeBar(_footerCanvasGroup, fromAlpha: 0f, toAlpha: 1f, dur, ease, interactive: true);

            if (_slideBarsIn)
            {
                _headerSlide = SlideBarIn(Header, _headerSlide, +1f, _headerSlideDuration, _headerSlideEase, _headerSlideDelay);
                _footerSlide = SlideBarIn(Footer, _footerSlide, -1f, _footerSlideDuration, _footerSlideEase, _footerSlideDelay);
            }
        }

        /// <summary>
        /// Slides <paramref name="bar"/> in from beyond its screen edge (<paramref name="edgeSign"/> +1 =
        /// top, −1 = bottom) to where it rests. The rest position is read LIVE, after landing any slide
        /// still running, rather than cached in Awake — safe-area / platform-footer setters may move a
        /// bar after Awake, and a cached value would slide it back to the wrong place.
        /// </summary>
        private Tween SlideBarIn(RectTransform bar, Tween running, float edgeSign, float duration, Ease ease, float delay)
        {
            if (bar == null) return null;
            if (running != null && running.IsActive()) running.Kill(complete: true);

            Vector2 rest = bar.anchoredPosition;
            float distance = bar.rect.height + _slideExtraDistance;
            bar.anchoredPosition = rest + new Vector2(0f, edgeSign * distance);

            return bar.DOAnchorPos(rest, Mathf.Max(0.01f, duration))
                .SetEase(ease)
                .SetDelay(delay)
                .SetLink(bar.gameObject);
        }

        /// <summary>Jumps any running bar slide to its rest position. Every hide path calls it, so a bar is never left off-screen.</summary>
        private void LandSlides()
        {
            if (_headerSlide != null && _headerSlide.IsActive()) _headerSlide.Kill(complete: true);
            if (_footerSlide != null && _footerSlide.IsActive()) _footerSlide.Kill(complete: true);
            _headerSlide = null;
            _footerSlide = null;
        }

        /// <summary>Duration the base ShowBars/HideBars fade uses — exposed so subclasses can sync follow-up animations.</summary>
        protected float BarFadeInDuration  => HudBarsConfig != null ? HudBarsConfig.easeInDuration  : 0.25f;
        protected float BarFadeOutDuration => HudBarsConfig != null ? HudBarsConfig.easeOutDuration : 0.25f;

        /// <summary>
        /// Fades header and footer out (alpha → 0) in place. Replaces the previous
        /// slide-out motion. Raycasts are disabled immediately so taps stop hitting
        /// the bars even before the fade completes.
        /// Called automatically on Hide(), and by _hideBarsEvent subscribers
        /// (e.g. before LevelFail panel, during tutorials).
        /// </summary>
        public virtual void HideBars()
        {
            float dur  = HudBarsConfig != null ? HudBarsConfig.easeOutDuration : 0.25f;
            Ease  ease = HudBarsConfig != null ? HudBarsConfig.easeOut : Ease.InCubic;
            LandSlides();
            FadeBar(_headerCanvasGroup, fromAlpha: 1f, toAlpha: 0f, dur, ease, interactive: false);
            FadeBar(_footerCanvasGroup, fromAlpha: 1f, toAlpha: 0f, dur, ease, interactive: false);
        }

        /// <summary>Fades ONLY the header in/out (footer untouched). Used e.g. to hide the header
        /// while the Wand booster's targeting/selection panel is open, then restore it on close.</summary>
        public virtual void SetHeaderVisible(bool visible)
            => SetHeaderVisible(visible, visible ? BarFadeInDuration : BarFadeOutDuration);

        /// <summary>As <see cref="SetHeaderVisible(bool)"/> but with an explicit fade duration, so a
        /// caller can sync the header fade with other elements (e.g. the Wand footer/tray fade).</summary>
        public virtual void SetHeaderVisible(bool visible, float duration)
        {
            Ease ease = HudBarsConfig != null ? (visible ? HudBarsConfig.easeIn : HudBarsConfig.easeOut)
                                              : (visible ? Ease.OutCubic : Ease.InCubic);
            FadeBar(_headerCanvasGroup, fromAlpha: visible ? 0f : 1f, toAlpha: visible ? 1f : 0f,
                    duration, ease, interactive: visible);
        }

        /// <summary>
        /// Instantly hides bars (alpha = 0) with no animation, killing any in-flight tween.
        /// Position is left untouched — bars only become invisible and non-interactable.
        /// Called by GameManager.LoadLevel() on the first level so bars never appear
        /// until the tutorial is dismissed and _showBarsEvent fires.
        /// </summary>
        public virtual void SnapSectionsOffScreen()
        {
            LandSlides();
            SnapBar(_headerCanvasGroup, 0f);
            SnapBar(_footerCanvasGroup, 0f);
        }

        // Forces the start alpha so the tween is always visible — without this,
        // calling ShowBars on already-visible bars would tween from 1 → 1 (invisible).
        private static void FadeBar(CanvasGroup cg, float fromAlpha, float toAlpha,
            float duration, Ease ease, bool interactive)
        {
            if (cg == null) return;
            DOTween.Kill(cg);

            cg.alpha = fromAlpha;

            // Hidden bars must never catch taps. Enable raycasts only when fading in.
            cg.blocksRaycasts = interactive;
            cg.interactable   = interactive;

            cg.DOFade(toAlpha, duration).SetEase(ease).SetTarget(cg);
        }

        private static void SnapBar(CanvasGroup cg, float alpha)
        {
            if (cg == null) return;
            DOTween.Kill(cg);
            cg.alpha          = alpha;
            cg.blocksRaycasts = alpha > 0.001f;
            cg.interactable   = alpha > 0.001f;
        }

        // ── Utility ───────────────────────────────────────────────────────────

        public static void ResetToStretch(RectTransform rect)
        {
            rect.anchorMin        = Vector2.zero;
            rect.anchorMax        = Vector2.one;
            rect.offsetMin        = Vector2.zero;
            rect.offsetMax        = Vector2.zero;
            rect.pivot            = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.localPosition    = Vector3.zero;
            rect.localRotation    = Quaternion.identity;
            rect.localScale       = Vector3.one;
        }
    }
}
