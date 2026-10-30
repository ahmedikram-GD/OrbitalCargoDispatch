using GameKit.Architecture.Events;
using GameKit.Architecture.Variables;
using OrbitalCargo.Config;
using UnityEngine;

namespace OrbitalCargo.Economy
{
    /// <summary>
    /// The one upgrade in the pitch: the grav-sled (carry 3 crates instead of 1). Ownership persists in
    /// <c>vDBBool_GravSled</c>; the price and capacities come from <c>ShiftConfig_Pitch</c>. The shift
    /// controller applies <see cref="CarryCapacity"/> to the player when a shift begins.
    /// </summary>
    [CreateAssetMenu(fileName = "UpgradeService", menuName = "OrbitalCargo/Economy/Upgrade Service")]
    public class UpgradeService : ScriptableObject
    {
        [Tooltip("vDBBool_GravSled — persistent ownership flag.")]
        [SerializeField] private DBBool gravSled;
        [Tooltip("ShiftConfig_Pitch — GravSledPrice, CarryCapacityBase, CarryCapacityGravSled.")]
        [SerializeField] private ShiftConfigSO config;
        [SerializeField] private CreditsService credits;
        [Tooltip("e_UpgradePurchased — raised after a successful purchase.")]
        [SerializeField] private GameEvent onUpgradePurchased;

        public bool GravSledOwned => gravSled != null && gravSled.GetValue();
        public int GravSledPrice => config != null ? config.GravSledPrice : 0;
        public bool CanBuyGravSled => !GravSledOwned && credits != null && credits.Balance >= GravSledPrice;

        public int CarryCapacity => config == null ? 1
            : GravSledOwned ? config.CarryCapacityGravSled : config.CarryCapacityBase;

        public bool TryBuyGravSled()
        {
            if (gravSled == null || GravSledOwned || credits == null || !credits.TrySpend(GravSledPrice)) return false;
            gravSled.SetValue(true);
            if (onUpgradePurchased != null) onUpgradePurchased.Invoke();
            return true;
        }
    }
}
