using UnityEngine;
using GameKit.Architecture.Events;
using Sirenix.OdinInspector;

namespace GameKit.Architecture.Variables
{

[CreateAssetMenu(fileName = "vDBIntWithEvent_", menuName = "ProjectCore/Variables/Persistent/DBIntWithEvent")]
public class DBIntWithEvent : DBInt
{
    [SerializeField] public GameEvent GameEvent;

    [Button]
    public override void Decrement(int _decrement)
    {
        base.Decrement(_decrement);
        GameEvent.Invoke();
    }

    public override void Increment(int _increment)
    {
        base.Increment(_increment);
        GameEvent.Invoke();
    }

    public override void SetValue(int value)
    {
        base.SetValue(value);
        GameEvent.Invoke();
    }
}
      

}