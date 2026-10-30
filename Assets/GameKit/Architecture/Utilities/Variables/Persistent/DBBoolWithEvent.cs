using UnityEngine;
using GameKit.Architecture.Events;

namespace GameKit.Architecture.Variables
{

[CreateAssetMenu(fileName = "vDBBoolWithEvent_", menuName = "ProjectCore/Variables/Persistent/DBBoolWithEvent")]
public class DBBoolWithEvent : DBBool
{
    public GameEvent GameEvent;
    public override void SetValue(bool value)
    {
        base.SetValue(value);
        GameEvent.Invoke();
    }
}

}