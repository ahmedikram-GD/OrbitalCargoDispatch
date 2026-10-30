using DG.Tweening;
using OrbitalCargo.Config;
using OrbitalCargo.Crates;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalCargo.UI
{
    /// <summary>
    /// "Added to collection: Alien Egg" card (KICKOFF: a pop-up is enough instead of the full book).
    /// Pops in when e_ItemCollected fires, holds, fades out.
    /// </summary>
    [AddComponentMenu("OrbitalCargo/UI/Collection Popup View")]
    public sealed class CollectionPopupView : MonoBehaviour
    {
        [SerializeField] private GameEventWithHiddenItem onItemCollected;
        [SerializeField] private RectTransform card;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI title;
        [SerializeField] private TextMeshProUGUI subtitle;
        [Tooltip("{0} = item name.")]
        [SerializeField] private string titleFormat = "Added to collection";
        [Tooltip("{0} = item name, {1} = collection set.")]
        [SerializeField] private string subtitleFormat = "{0}  ·  {1}";
        [Min(0.5f)] [SerializeField] private float holdSeconds = 2.5f;

        private Sequence _show;

        private void Awake()
        {
            if (group != null) group.alpha = 0f;
        }

        private void OnEnable()
        {
            if (onItemCollected != null) onItemCollected.Subscribe(HandleItemCollected);
        }

        private void OnDisable()
        {
            if (onItemCollected != null) onItemCollected.UnSubscribe(HandleItemCollected);
            _show?.Kill();
        }

        private void HandleItemCollected(HiddenItemSO item)
        {
            if (card == null || group == null || item == null) return;

            if (title != null) title.text = string.Format(titleFormat, item.DisplayName);
            if (subtitle != null) subtitle.text = string.Format(subtitleFormat, item.DisplayName, item.CollectionSet);
            if (icon != null)
            {
                if (item.Icon != null) icon.sprite = item.Icon;
                icon.color = item.Icon != null ? Color.white : item.IconTint;
            }

            _show?.Kill();
            group.alpha = 0f;
            card.localScale = Vector3.one * 0.6f;
            _show = DOTween.Sequence()
                .Join(group.DOFade(1f, 0.2f))
                .Join(card.DOScale(1f, 0.35f).SetEase(Ease.OutBack))
                .AppendInterval(holdSeconds)
                .Append(group.DOFade(0f, 0.4f))
                .SetLink(gameObject);
        }
    }
}
