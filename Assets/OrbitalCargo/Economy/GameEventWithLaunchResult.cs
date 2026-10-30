using GameKit.Architecture.Events;
using UnityEngine;

namespace OrbitalCargo.Economy
{
    /// <summary>Kit typed event channel carrying a <see cref="LaunchResult"/> (e_ShuttleLaunched).</summary>
    [CreateAssetMenu(fileName = "e_", menuName = "OrbitalCargo/Events/Launch Result Event")]
    public class GameEventWithLaunchResult : GameEventWithParam<LaunchResult>
    {
    }
}
