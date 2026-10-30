using GameKit.Architecture.Events;
using UnityEngine;

namespace OrbitalCargo.Crates
{
    /// <summary>Kit typed event channel carrying a <see cref="Crate"/> (e_CratePicked, e_CratePlaced...).</summary>
    [CreateAssetMenu(fileName = "e_", menuName = "OrbitalCargo/Events/Crate Event")]
    public class GameEventWithCrate : GameEventWithParam<Crate>
    {
    }
}
