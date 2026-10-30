using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using System;
using Sirenix.OdinInspector;

namespace GameKit.AppFlow
{

public class VerticalGroupAnimator : MonoBehaviour
{
    [Header("Animation Settings")]
    public float fadeDuration = 0.25f;
    public float scaleDuration = 0.35f;
    public float delayBetweenItems = 0.12f;
    public Vector3 hiddenScale = new Vector3(0.8f, 0.8f, 1f);
    public Ease showEase = Ease.OutBack;
    public Ease hideEase = Ease.InBack;

    private List<CanvasGroup> sections = new List<CanvasGroup>();
    private Coroutine animationCoroutine;

    private void Awake()
    {
        CollectSections();
    }

    private void CollectSections()
    {
        sections.Clear();
        foreach (Transform child in transform)
        {
            CanvasGroup cg = child.GetComponent<CanvasGroup>();
            if (cg == null) cg = child.gameObject.AddComponent<CanvasGroup>();

            // Initialize hidden
            cg.alpha = 0;
            cg.interactable = false;
            cg.blocksRaycasts = false;
            child.localScale = hiddenScale;

            sections.Add(cg);
        }
    }
    [Button]
    public void Show(Action onComplete = null, bool animated = true)
    {
        if (animationCoroutine != null) StopCoroutine(animationCoroutine);

        // ✅ Reset all sections immediately to hidden state
        foreach (var section in sections)
        {
            section.alpha = 0;
            section.transform.localScale = hiddenScale;
            section.interactable = false;
            section.blocksRaycasts = false;
        }

        animationCoroutine = StartCoroutine(AnimateSections(true, onComplete, animated));
    }

    [Button]
    public void Hide(Action onComplete = null, bool animated = true)
    {
        if (animationCoroutine != null) StopCoroutine(animationCoroutine);
        animationCoroutine = StartCoroutine(AnimateSections(false, onComplete, animated));
    }

    private IEnumerator AnimateSections(bool show, Action onComplete, bool animated)
    {
        float targetAlpha = show ? 1 : 0;
        Vector3 targetScale = show ? Vector3.one : hiddenScale;
        Ease ease = show ? showEase : hideEase;

        for (int i = 0; i < sections.Count; i++)
        {
            var section = sections[i];
            var t = section.transform;

            if (animated)
            {
                if (show)
                {
                    // Reset to hidden state before showing
                    section.alpha = 0;
                    t.localScale = hiddenScale;
                }

                section.DOFade(targetAlpha, fadeDuration);
                t.DOScale(targetScale, scaleDuration).SetEase(ease);
            }
            else
            {
                section.alpha = targetAlpha;
                t.localScale = targetScale;
            }

            section.interactable = show;
            section.blocksRaycasts = show;

            if (animated)
                yield return new WaitForSeconds(delayBetweenItems);
        }

        if (animated)
            yield return new WaitForSeconds(Mathf.Max(fadeDuration, scaleDuration));

        onComplete?.Invoke();
        animationCoroutine = null;
    }

}

}