using GameKit.AppFlow;
using GameKit.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalCargo.UI
{
    /// <summary>
    /// End-of-shift card (pooled "ShiftSummary"): the shift's tallies, then the three choices. Passive —
    /// <c>ShiftSummaryState</c> fills it and binds the buttons; rows cascade in with the kit
    /// <see cref="VerticalGroupAnimator"/>.
    /// </summary>
    [AddComponentMenu("OrbitalCargo/UI/Shift Summary View")]
    public sealed class ShiftSummaryView : UIBase
    {
        [Header("Stats")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text earnedText;
        [SerializeField] private TMP_Text launchesText;
        [SerializeField] private TMP_Text shippedText;
        [SerializeField] private TMP_Text itemsText;
        [SerializeField] private TMP_Text finesText;
        [SerializeField] private TMP_Text balanceText;

        [Header("Choices")]
        [SerializeField] private Button doubleButton;
        [SerializeField] private TMP_Text doubleLabel;
        [SerializeField] private Button buyButton;
        [SerializeField] private TMP_Text buyLabel;
        [SerializeField] private Button nextButton;

        [Header("Animation")]
        [SerializeField] private VerticalGroupAnimator rows;

        public Button DoubleButton => doubleButton;
        public Button BuyButton => buyButton;
        public Button NextButton => nextButton;

        // ── UIBase ─────────────────────────────────────────────────────────────

        public override void Show(bool playDefaultAnimation = false, bool makeStateinteractable = true)
        {
            base.Show(playDefaultAnimation, makeStateinteractable);
            if (rows != null) rows.Show();
        }

        // ── Public API ─────────────────────────────────────────────────────────

        public void SetStats(int shiftNumber, int earned, int launches, int shipped, int items, int fines)
        {
            if (titleText != null) titleText.text = $"SHIFT {shiftNumber} COMPLETE";
            if (earnedText != null) earnedText.text = $"{earned} ¢";
            if (launchesText != null) launchesText.text = launches.ToString();
            if (shippedText != null) shippedText.text = shipped.ToString();
            if (itemsText != null) itemsText.text = items.ToString();
            if (finesText != null) finesText.text = fines > 0 ? $"-{fines} ¢" : "0 ¢";
        }

        public void SetDouble(bool interactable, bool claimed, bool playing, int earned)
        {
            doubleButton.interactable = interactable;
            if (doubleLabel == null) return;
            doubleLabel.text = claimed ? $"Doubled! +{earned} ¢"
                             : playing ? "Ad playing…"
                             : $"2× payout  (watch ad)  +{earned} ¢";
        }

        public void SetBuy(bool interactable, bool owned, int price)
        {
            buyButton.interactable = interactable;
            if (buyLabel != null) buyLabel.text = owned ? "Grav-Sled owned (carry 3)" : $"Buy Grav-Sled  ({price} ¢)";
        }

        public void SetBalance(int balance)
        {
            if (balanceText != null) balanceText.text = $"Balance  {balance} ¢";
        }
    }
}
