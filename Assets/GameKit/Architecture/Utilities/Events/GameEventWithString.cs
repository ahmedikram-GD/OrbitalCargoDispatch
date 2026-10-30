using Sirenix.OdinInspector;
using UnityEngine;

namespace GameKit.Architecture.Events
{
[CreateAssetMenu(fileName = "GameEventWithString", menuName = "ProjectCore/Events/GameEventWithString")]
public class GameEventWithString : GameEventWithParam<string>
{
        [Button]
        [GUIColor(1, 1, 0.5f)]
        public override void Raise(string t)
        {
            base.Raise(t);
        }
    
}
   
}