using GameKit.Architecture.Events;
using OrbitalCargo.Config;
using UnityEngine;

namespace OrbitalCargo.Crates
{
    /// <summary>Kit typed event channel carrying a <see cref="HiddenItemSO"/> (e_ItemCollected).</summary>
    [CreateAssetMenu(fileName = "e_", menuName = "OrbitalCargo/Events/Hidden Item Event")]
    public class GameEventWithHiddenItem : GameEventWithParam<HiddenItemSO>
    {
    }
}
