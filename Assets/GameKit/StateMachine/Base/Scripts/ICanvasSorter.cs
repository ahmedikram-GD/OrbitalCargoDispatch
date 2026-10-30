using System.Collections.Generic;
using UnityEngine;

namespace GameKit.StateMachine
{

public interface ICanvasSorter
{
    List<Canvas> childCanvases { get; set; }
    void ApplySortingOrder(int startOrder);
}

}