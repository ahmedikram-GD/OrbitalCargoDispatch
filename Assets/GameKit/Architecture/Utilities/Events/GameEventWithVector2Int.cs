using GameKit.Architecture.Events;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GameKit.Architecture
{

[CreateAssetMenu(fileName = "GameEventWithVector2Int", menuName = "ProjectCore/Events/GameEventWithVector2Int")]
public class GameEventWithVector2Int : GameEventWithParam<Vector2Int>
{
    [Button]
    [GUIColor(1, 1, 0.5f)]
    public override void Raise(Vector2Int t)
    {
        base.Raise(t);
    }
}

}