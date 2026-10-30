using UnityEngine;
using System.Collections;
using Sirenix.OdinInspector;
using System.Collections.Generic;

namespace GameKit.Art
{

public class UISequentialScaler : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private UISequentialScaleConfigSO config;

    [Header("Elements to Animate")]
    [SerializeField] private List<RectTransform> elements = new List<RectTransform>();

    private Coroutine playRoutine;
    private int completedCount = 0;

    private void OnEnable()
    {
        PlaySequence();
    }

    [Button("Play Sequence (Test)")]
    public void PlaySequence()
    {
        ResetElements();
        completedCount = 0;

        if (playRoutine != null)
            StopCoroutine(playRoutine);

        playRoutine = StartCoroutine(PlayScaleSequence());
    }

    private void ResetElements()
    {
        foreach (var rt in elements)
        {
            if (rt == null) continue;

            rt.gameObject.SetActive(false);
            rt.localScale = Vector3.one * config.startScale;
        }
    }

    private IEnumerator PlayScaleSequence()
    {
        yield return null;

        // === CASE 1: No delay → animate all at once ===
        if (config.delayBetween <= 0f)
        {
            foreach (var rt in elements)
            {
                if (rt == null) continue;

                rt.gameObject.SetActive(true);
                StartCoroutine(ScaleUp(rt));
            }
            yield break;
        }

        // === CASE 2: Sequential animation ===
        foreach (var rt in elements)
        {
            if (rt == null) continue;

            rt.gameObject.SetActive(true);
            StartCoroutine(ScaleUp(rt));

            yield return new WaitForSeconds(config.delayBetween);
        }
    }

    private IEnumerator ScaleUp(RectTransform rt)
    {
        float time = 0f;

        Vector3 start = Vector3.one * config.startScale;
        Vector3 end = Vector3.one;

        while (time < config.scaleDuration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / config.scaleDuration);
            float eased = config.scaleCurve.Evaluate(t);

            rt.localScale = Vector3.LerpUnclamped(start, end, eased);

            yield return null;
        }

        rt.localScale = end;
    }
}

}