using Sirenix.OdinInspector;
using UnityEngine;

namespace GameKit.Architecture.Events
{
[CreateAssetMenu(fileName = "GameEventWithBool", menuName = "ProjectCore/Events/GameEventWithBool")]
public class GameEventWithBool : GameEventWithParam<bool>
{
        [Button]
        [GUIColor(1, 1, 0.5f)]
        public override void Raise(bool t)
        {
            base.Raise(t);
        }
    
}
   
}