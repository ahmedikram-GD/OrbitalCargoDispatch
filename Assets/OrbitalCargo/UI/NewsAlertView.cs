using DG.Tweening;
using GameKit.Architecture.Events;
using TMPro;
using UnityEngine;

namespace OrbitalCargo.UI
{
    /// <summary>
    /// "BREAKING" news ticker that drops in at the top of the HUD when e_NewsAlert fires, holds, and
    /// slides back out. A newer alert replaces the current one.
    /// </summary>
    [AddComponentMenu("OrbitalCargo/UI/News Alert View")]
    public sealed class NewsAlertView : MonoBehaviour
    {
        [SerializeField] private GameEventWithString onNewsAlert;
        [Tooltip("The banner (anchored top-centre); slides from hiddenY to shownY.")]
        [SerializeField] private RectTransform banner;
        [SerializeField] private TextMeshProUGUI label;

        [SerializeField] private float shownY = -150f;
        [SerializeField] private float hiddenY = 140f;
        [Min(0.05f)] [SerializeField] private float slideDuration = 0.35f;
        [Min(0.5f)] [SerializeField] private float holdSeconds = 3.5f;

        private Sequence _show;

        private void Awake()
        {
            if (banner != null) banner.anchoredPosition = new Vector2(banner.anchoredPosition.x, hiddenY);
        }

        private void OnEnable()
        {
            if (onNewsAlert != null) onNewsAlert.Subscribe(HandleNewsAlert);
        }

        private void OnDisable()
        {
            if (onNewsAlert != null) onNewsAlert.UnSubscribe(HandleNewsAlert);
            _show?.Kill();
        }

        private void HandleNewsAlert(string line)
        {
            if (banner == null) return;
            if (label != null) label.text = line;

            _show?.Kill();
            banner.anchoredPosition = new Vector2(banner.anchoredPosition.x, hiddenY);
            _show = DOTween.Sequence()
                .Append(banner.DOAnchorPosY(shownY, slideDuration).SetEase(Ease.OutBack))
                .Append(banner.DOPunchScale(Vector3.one * 0.06f, 0.3f, 6))
                .AppendInterval(holdSeconds)
                .Append(banner.DOAnchorPosY(hiddenY, slideDuration).SetEase(Ease.InBack))
                .SetLink(gameObject);
        }
    }
}
