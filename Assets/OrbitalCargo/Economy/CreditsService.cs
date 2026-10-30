using GameKit.Architecture.Events;
using GameKit.Architecture.Variables;
using UnityEngine;

namespace OrbitalCargo.Economy
{
    /// <summary>
    /// The one place credits change. Wraps the persistent <c>vDBInt_Credits</c> and raises
    /// <c>e_CreditsChanged</c> so the HUD's kit <c>CoinsWidget</c> refreshes. A deferred add skips
    /// the event because the coin trail will reveal the new balance as the coins land.
    /// </summary>
    [CreateAssetMenu(fileName = "CreditsService", menuName = "OrbitalCargo/Economy/Credits Service")]
    public class CreditsService : ScriptableObject
    {
        [Tooltip("vDBInt_Credits — the persistent balance.")]
        [SerializeField] private DBInt credits;
        [Tooltip("e_CreditsChanged — snap the HUD counter to the new balance.")]
        [SerializeField] private GameEvent balanceChanged;

        public int Balance => credits != null ? credits.GetValue() : 0;

        /// <summary>
        /// Change the balance by <paramref name="amount"/> (negative = fine; the balance never goes
        /// below zero). <paramref name="deferDisplay"/> = the caller is playing a coin trail that will
        /// reveal the new value, so the HUD isn't told yet.
        /// </summary>
        public void Add(int amount, bool deferDisplay = false)
        {
            if (credits == null || amount == 0) return;
            credits.SetValue(Mathf.Max(0, credits.GetValue() + amount));
            if (!deferDisplay) RaiseChanged();
        }

        public bool TrySpend(int amount)
        {
            if (credits == null || amount < 0 || credits.GetValue() < amount) return false;
            credits.SetValue(credits.GetValue() - amount);
            RaiseChanged();
            return true;
        }

        private void RaiseChanged()
        {
            if (balanceChanged != null) balanceChanged.Invoke();
        }
    }
}
