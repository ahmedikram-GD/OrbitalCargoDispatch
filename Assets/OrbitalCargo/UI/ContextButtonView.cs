using OrbitalCargo.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalCargo.UI
{
    /// <summary>
    /// The bottom-right context button (Pick up / Place / Scan / Launch). Shows whatever the
    /// <see cref="CrateInteractor"/> currently offers, hides (and stops eating touches) when there is
    /// nothing to do, and turns a tap into an interact request on the input provider — the same latch
    /// the E key feeds. Turns red when the prompt is a warning (wrong bay). Replaces Silly-Justice's
    /// InteractionButtonBinder + HeldItemButtonSwitcher pair.
    /// </summary>
    [AddComponentMenu("OrbitalCargo/UI/Context Button View")]
    public sealed class ContextButtonView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TextMeshProUGUI label;
        [Tooltip("On this object. Alpha 0 + no raycasts when hidden, so the look area gets the touch.")]
        [SerializeField] private CanvasGroup group;

        [Tooltip("Button fill while the prompt is a warning (crate aimed at the wrong bay).")]
        [SerializeField] private Color warningColor = new Color(0.95f, 0.22f, 0.18f, 0.95f);

        [Header("Scene wiring (set by SceneSetup)")]
        [SerializeField] private CrateInteractor interactor;
        [SerializeField] private InteractionInputProvider input;

        private Image _background;
        private Color _normalColor;

        public Button Button => button;
        public RectTransform Rect => (RectTransform)transform;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            _background = button != null ? button.targetGraphic as Image : null;
            if (_background != null) _normalColor = _background.color;
        }

        private void OnEnable()
        {
            if (button != null) button.onClick.AddListener(OnButtonClicked);
            if (interactor == null) return;
            interactor.PromptChanged += HandlePromptChanged;
            HandlePromptChanged(interactor.Prompt, interactor.PromptEnabled);
        }

        private void OnDisable()
        {
            if (button != null) button.onClick.RemoveListener(OnButtonClicked);
            if (interactor != null) interactor.PromptChanged -= HandlePromptChanged;
        }

        // ── Handlers ───────────────────────────────────────────────────────────

        private void HandlePromptChanged(string text, bool enabled)
        {
            bool visible = !string.IsNullOrEmpty(text);
            if (group != null)
            {
                group.alpha = visible ? 1f : 0f;
                group.blocksRaycasts = visible;
                group.interactable = visible && enabled;
            }
            if (button != null) button.interactable = enabled;
            if (label != null) label.text = text;
            if (_background != null)
                _background.color = interactor != null && interactor.PromptIsWarning ? warningColor : _normalColor;
        }

        private void OnButtonClicked()
        {
            if (input != null) input.RequestInteract();
        }
    }
}
