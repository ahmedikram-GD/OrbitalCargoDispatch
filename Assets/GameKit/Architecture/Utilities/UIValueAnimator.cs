using System.Collections;
using TMPro;
using UnityEngine;

namespace GameKit.Architecture
{

public static class UIValueAnimator
{
    public static void AnimateIntValue(MonoBehaviour runner, TextMeshProUGUI text, int fromValue, int toValue, float duration = 1f)
    {
        runner.StartCoroutine(AnimateTextRoutine(text, fromValue, toValue, duration));
    }

    private static IEnumerator AnimateTextRoutine(TextMeshProUGUI text, int from, int to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            int currentValue = Mathf.RoundToInt(Mathf.Lerp(from, to, t));
            text.text = currentValue.ToString();
            yield return null;
        }
        text.text = to.ToString(); // Ensure final value is exact
    }
}
}