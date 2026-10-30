using GameKit.Architecture.Events;
using UnityEngine;

namespace GameKit.Architecture.Variables
{

[CreateAssetMenu(fileName = "vDBStringWithEvent_", menuName = "ProjectCore/Variables/Persistent/DBStringWithEvent")]
public class DBStringWithEvent : DBString
{
    [SerializeField] protected GameEvent GameEvent;
    public override void SetValue(string value)
    {
        base.SetValue(value);
        GameEvent.Invoke();
    }
}

}