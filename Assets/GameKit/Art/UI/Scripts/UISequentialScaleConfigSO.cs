using UnityEngine;

namespace GameKit.Art
{

[CreateAssetMenu(fileName = "UISequentialScaleConfig", menuName = "UI/Sequential Scale Config")]
public class UISequentialScaleConfigSO : ScriptableObject
{
    [Header("Animation Settings")]
    public float scaleDuration = 0.3f;
    public float delayBetween = 0f;
    public float startScale = 0.5f;
    public AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
}
}