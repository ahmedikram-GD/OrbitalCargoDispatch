using UnityEngine;
using GameKit.Architecture.Variables;
using GameKit.Architecture.Events;

namespace GameKit.Architecture.Variables
{

[CreateAssetMenu(fileName = "vStringWithEvent_", menuName = "ProjectCore/Variables/Non-Persistent/StringWithEvent")]
public class StringWithEvent : SharedString
{
    [SerializeField] protected GameEvent GameEvent;
    public override void SetValue(string value)
    {
        base.SetValue(value);
        GameEvent?.Invoke();
    }
}

}