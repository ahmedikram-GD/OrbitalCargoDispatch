using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalCargo.UI
{
    /// <summary>What the tutorial hand is miming.</summary>
    public enum TutorialGesture
    {
        None,
        Joystick, // circling on the move stick
        LookDrag, // sweeping left-right on the right half
        Tap,      // pressing the context button
        SwipeUp,  // flicking up on the right half (junk throw)
    }

    /// <summary>
    /// Passive onboarding overlay on the Controls canvas (so it hides with the controls on pause):
    /// a centred instruction card with a progress bar for the move/look lessons, a top-right objective
    /// card with the crate's colour swatch for the crate lessons, and a pointing hand that mimes the
    /// gesture over the real control it means. Everything is raycast-transparent — the touch controls
    /// underneath keep working. <c>TutorialDirector</c> decides what shows; this only presents it.
    /// </summary>
    [AddComponentMenu("OrbitalCargo/UI/Tutorial Overlay View")]
    public sealed class TutorialOverlayView : MonoBehaviour
    {
        [Header("Centre card (move / look)")]
        [SerializeField] private CanvasGroup card;
        [SerializeField] private TMP_Text cardTitle;
        [SerializeField] private TMP_Text cardBody;
        [Tooltip("Filled image (horizontal) that tracks the lesson's progress.")]
        [SerializeField] private Image cardProgress;

        [Header("Objective card (crate lessons)")]
        [SerializeField] private CanvasGroup objective;
        [SerializeField] private TMP_Text objectiveTitle;
        [SerializeField] private TMP_Text objectiveBody;
        [Tooltip("Square tinted the crate's colour — the colour is the lesson.")]
        [SerializeField] private Image objectiveSwatch;

        [Header("Hand")]
        [Tooltip("Pointing hand; its pivot is the fingertip.")]
        [SerializeField] private RectTransform hand;
        [SerializeField] private CanvasGroup handGroup;
        [Tooltip("Curved double arrow shown above the hand during the look gesture. Optional.")]
        [SerializeField] private RectTransform lookArrow;

        [Header("Hand targets (Controls prefab, set by PlayerRigBuilder)")]
        [Tooltip("Joystick Background — the hand circles its centre.")]
        [SerializeField] private RectTransform joystickTarget;
        [Tooltip("Context button — the hand taps it.")]
        [SerializeField] private RectTransform tapTarget;
        [Tooltip("Viewport point (0..1) the look sweep centres on.")]
        [SerializeField] private Vector2 lookPoint = new Vector2(0.7f, 0.4f);
        [Tooltip("Viewport point (0..1) the swipe-up starts from.")]
        [SerializeField] private Vector2 swipePoint = new Vector2(0.7f, 0.28f);

        [Header("Motion")]
        [Min(0f)] [SerializeField] private float fadeSeconds = 0.2f;
        [Min(0.1f)] [SerializeField] private float tapPeriod = 0.9f;
        [Tooltip("Canvas units the fingertip travels toward the button on each press.")]
        [SerializeField] private Vector2 tapApproach = new Vector2(40f, -40f);
        [Min(0f)] [SerializeField] private float joystickRadius = 60f;
        [Min(0.1f)] [SerializeField] private float joystickPeriod = 1.6f;
        [Min(0f)] [SerializeField] private float lookAmplitude = 180f;
        [Min(0.1f)] [SerializeField] private float lookPeriod = 1.8f;
        [Min(0f)] [SerializeField] private float swipeDistance = 280f;
        [Min(0.1f)] [SerializeField] private float swipePeriod = 1.1f;
        [Tooltip("Scale punch when a card appears or its content changes.")]
        [Min(0f)] [SerializeField] private float punch = 0.12f;

        private RectTransform _root;
        private bool _cardShown;
        private bool _objectiveShown;
        private TutorialGesture _gesture;
        private float _gestureTime;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            _root = (RectTransform)transform;
            SetAlpha(card, 0f);
            SetAlpha(objective, 0f);
            SetAlpha(handGroup, 0f);
            if (lookArrow != null) lookArrow.gameObject.SetActive(false);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            Fade(card, _cardShown, dt);
            Fade(objective, _objectiveShown, dt);
            Fade(handGroup, _gesture != TutorialGesture.None, dt);

            if (_gesture == TutorialGesture.None || hand == null) return;
            _gestureTime += dt;
            AnimateHand();
        }

        // ── Public API ─────────────────────────────────────────────────────────

        public void ShowCard(string title, string body)
        {
            if (cardTitle != null) cardTitle.text = title;
            if (cardBody != null) cardBody.text = body;
            SetCardProgress(0f);
            _cardShown = true;
            Punch(card);
        }

        public void SetCardProgress(float progress)
        {
            if (cardProgress != null) cardProgress.fillAmount = Mathf.Clamp01(progress);
        }

        public void HideCard() => _cardShown = false;

        public void ShowObjective(string title, string body, Color swatch)
        {
            bool changed = !_objectiveShown || (objectiveBody != null && objectiveBody.text != body);
            if (objectiveTitle != null) objectiveTitle.text = title;
            if (objectiveBody != null) objectiveBody.text = body;
            if (objectiveSwatch != null) objectiveSwatch.color = swatch;
            _objectiveShown = true;
            if (changed) Punch(objective);
        }

        public void HideObjective() => _objectiveShown = false;

        public void SetGesture(TutorialGesture gesture)
        {
            if (gesture == _gesture) return;
            _gesture = gesture;
            _gestureTime = 0f;
            if (lookArrow != null) lookArrow.gameObject.SetActive(gesture == TutorialGesture.LookDrag);
            if (hand != null) hand.localScale = Vector3.one;
            if (gesture != TutorialGesture.None) AnimateHand();
        }

        public void HideAll()
        {
            HideCard();
            HideObjective();
            SetGesture(TutorialGesture.None);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void AnimateHand()
        {
            float t = _gestureTime;
            switch (_gesture)
            {
                case TutorialGesture.Joystick:
                {
                    float a = t / joystickPeriod * Mathf.PI * 2f;
                    hand.localPosition = CentreOf(joystickTarget) + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * joystickRadius;
                    break;
                }
                case TutorialGesture.LookDrag:
                {
                    Vector2 centre = ViewportToLocal(lookPoint);
                    hand.localPosition = centre + Vector2.right * (Mathf.Sin(t / lookPeriod * Mathf.PI * 2f) * lookAmplitude);
                    if (lookArrow != null) lookArrow.localPosition = centre + Vector2.up * 190f;
                    break;
                }
                case TutorialGesture.Tap:
                {
                    // Press quickly, release, rest: fingertip moves onto the button and the hand squashes.
                    float phase = Mathf.Repeat(t / tapPeriod, 1f);
                    float press = phase < 0.25f ? phase / 0.25f : phase < 0.5f ? 1f - (phase - 0.25f) / 0.25f : 0f;
                    hand.localPosition = CentreOf(tapTarget) + tapApproach * (1f - press);
                    hand.localScale = Vector3.one * (1f - 0.12f * press);
                    break;
                }
                case TutorialGesture.SwipeUp:
                {
                    float phase = Mathf.Repeat(t / swipePeriod, 1f);
                    float travel = 1f - (1f - phase) * (1f - phase); // ease-out flick
                    hand.localPosition = ViewportToLocal(swipePoint) + Vector2.up * (travel * swipeDistance);
                    if (handGroup != null) handGroup.alpha = Mathf.Min(handGroup.alpha, phase > 0.7f ? (1f - phase) / 0.3f : 1f);
                    break;
                }
            }
        }

        private Vector2 CentreOf(RectTransform target)
        {
            if (target == null || _root == null) return Vector2.zero;
            return _root.InverseTransformPoint(target.TransformPoint(target.rect.center));
        }

        private Vector2 ViewportToLocal(Vector2 viewport)
        {
            if (_root == null) return Vector2.zero;
            Rect r = _root.rect;
            return new Vector2(Mathf.Lerp(r.xMin, r.xMax, viewport.x), Mathf.Lerp(r.yMin, r.yMax, viewport.y));
        }

        private void Fade(CanvasGroup group, bool visible, float dt)
        {
            if (group == null) return;
            float target = visible ? 1f : 0f;
            group.alpha = fadeSeconds > 0f ? Mathf.MoveTowards(group.alpha, target, dt / fadeSeconds) : target;
        }

        private void Punch(CanvasGroup group)
        {
            if (group == null || punch <= 0f) return;
            var t = group.transform;
            t.DOKill();
            t.localScale = Vector3.one;
            t.DOPunchScale(Vector3.one * punch, 0.3f, 6, 0.6f).SetLink(gameObject);
        }

        private static void SetAlpha(CanvasGroup group, float alpha)
        {
            if (group == null) return;
            group.alpha = alpha;
            group.blocksRaycasts = false;
            group.interactable = false;
        }
    }
}
