using System;
using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;
using DG.Tweening;
using GameKit.Architecture.PoolSystem;
using GameKit.Haptics;
using THEBADDEST.SoundSystem;
using GameKit.UI.Header;
using Random = UnityEngine.Random;

namespace GameKit.UI.Animations
{

public class UICollector : MonoBehaviour
{
    public static Action<string,RectTransform, RectTransform,int> PlayTrailAction;
    

    [SerializeField]
    private Transform coinsHolder;

    //public static Action AnimateLifeHeaderAction;

    [Header("References")]
    public PoolManagerSO poolManager;
    private string poolId;
    public Canvas canvas;
    public Camera uiCamera;

    [Header("Movement Settings")]
    public float coinScale = 1f;
    public int coinCount = 10;

    public float spreadDuration = 0.4f;       // slow spread
    public float moveToTargetDuration = 0.2f; // fast movement
    public float spawnInterval = 0.05f;
    public float randomSpread = 60f;

    [Header("Test Setup")] 
    [SerializeField] private bool testing = false;
    [SerializeField]private string testPoolId;
    [SerializeField] private RectTransform testStartRect;
    [SerializeField] private RectTransform testTargetRect;
    
    private RectTransform startParticles;
    private RectTransform targetParticles;

    private bool targetParticlesActivated = false;
    
    [SerializeField] private SoundService soundService;
    [SerializeField] private HapticHandlerSO hapticHandlerSo;
    
    private void OnEnable()
    {
        PlayTrailAction += PlayTrail;
    }

    private void OnDisable()
    {
        PlayTrailAction -= PlayTrail;
    }

    public void PlayTrail(string poolId, RectTransform source, RectTransform target, int trailLength)
    {
        Vector2 startAnchored = ConvertToCanvasPosition(source);
        Vector2 targetAnchored = ConvertToCanvasPosition(target);

        StartCoroutine(SpawnAndAnimate(poolId, source, target, startAnchored, targetAnchored, trailLength));
    }


    [Button("Test Coin Trail")]
    private void TestFromInspector()
    {
        if (testStartRect == null || testTargetRect == null)
        {
            // Debug.LogWarning("Test Rects not assigned!");
            return;
        }

        PlayTrail(testPoolId,testStartRect, testTargetRect,coinCount);
    }

    private IEnumerator SpawnAndAnimate(
        string poolId,
        RectTransform source,
        RectTransform target,
        Vector2 startAnchored,
        Vector2 targetAnchored,
        int count)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject coinObj = poolManager.Get(poolId);
            RectTransform coin = coinObj.GetComponent<RectTransform>();

            coin.SetParent(coinsHolder.transform, false);
            coin.localRotation = Quaternion.identity;
            coin.anchoredPosition = startAnchored;
            coin.gameObject.SetActive(true);
            soundService.Play("CollectSpawn");
            coin.localScale = Vector3.one * coinScale * 0.8f;
            coin.DOScale(coinScale * 1.3f, 0.15f).SetEase(Ease.OutBack).OnComplete(() =>
            {
                coin.DOScale(coinScale, 0.15f).SetEase(Ease.InOutSine);
            });

            Vector2 spreadOffset = Random.insideUnitCircle * randomSpread;
            Vector2 spreadTarget = startAnchored + spreadOffset;

            bool isFirst = (i == 0);
            bool isLast = (i == count - 1);

            StartCoroutine(Animate(poolId, coin, spreadTarget, targetAnchored, isFirst, isLast, target));
            hapticHandlerSo.ApplyHapticFeedback(HapticIntensity.Soft);
            yield return new WaitForSeconds(spawnInterval);
        }
    }


    private IEnumerator Animate(
        string poolId,
        RectTransform coin,
        Vector2 spreadTarget,
        Vector2 finalTarget,
        bool isFirstCoin,
        bool isLastCoin,
        RectTransform targetRect)
    {
        yield return coin.DOAnchorPos(spreadTarget, spreadDuration)
            .SetEase(Ease.OutQuad)
            .WaitForCompletion();

        Tween moveTween = coin.DOAnchorPos(finalTarget, moveToTargetDuration)
            .SetEase(Ease.InQuad);

        if (isFirstCoin)
        {
            yield return new WaitForSeconds(moveToTargetDuration * 0.9f);
            if (poolId == "Life" || poolId == "LifePrefab")
                HeaderSignals.RaiseLivesGained(HeaderAnimParams.Default);
            else
                HeaderSignals.RaiseCoinsGained(HeaderAnimParams.Default);
            hapticHandlerSo.ApplyHapticFeedback(HapticIntensity.Hard);
        }

        yield return moveTween.WaitForCompletion();

        coin.anchoredPosition = finalTarget;
        poolManager.Release(poolId, coin.gameObject);
    }
    

    private Vector2 ConvertToCanvasPosition(RectTransform target)
    {
    
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, target.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            screenPoint,
            uiCamera,
            out Vector2 localPoint
        );
        return localPoint;
    }
}

}