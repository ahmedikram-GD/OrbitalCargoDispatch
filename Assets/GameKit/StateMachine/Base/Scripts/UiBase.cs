using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using GameKit.Architecture.Variables;
using UnityEngine;
using GameKit.StateMachine;

namespace GameKit.UI
{
    public abstract class UIBase : MonoBehaviour, IShowable , ICanvasSorter
    {
        [SerializeField] protected StateAnimationConfig animationConfig;
        [SerializeField] protected RectTransform UIPanel;
        [SerializeField] private Int currentStateSortingOrder;
        
        [SerializeField] protected Canvas _canvas;
        [SerializeField] protected CanvasGroup _canvasGroup;
        public List<Canvas> childCanvases { get; set; }

        private UiAnimationSystem _animationSystem;
        
        // Track animation state
        private Action _pendingHideCallback;
        private bool _isHiding;
        
        protected virtual void Awake()
        {
            _canvas = GetComponent<Canvas>();
            _canvasGroup = GetComponent<CanvasGroup>();

            if (_canvas != null && GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }

            if (_canvas != null && _canvas.worldCamera == null) _canvas.worldCamera = Camera.main;
            if (UIPanel == null) UIPanel = GetComponent<RectTransform>();

            // Cache once — excludes the root canvas so it keeps its assigned sortingOrder
            childCanvases = new List<Canvas>();
            foreach (var c in GetComponentsInChildren<Canvas>(includeInactive: true))
            {
                if (c != _canvas) childCanvases.Add(c);
            }

            _animationSystem = new UiAnimationSystem(this, _canvasGroup, UIPanel, animationConfig);
        }

        public virtual void Show(bool playDefaultAnimation = false, bool makeStateinteractable = true)
        {
            print("UI base show called");
            if(currentStateSortingOrder != null)
            {
                _canvas.sortingOrder = currentStateSortingOrder.GetValue();
                ApplySortingOrder(_canvas.sortingOrder);
            }
            
            
            //_canvas.planeDistance = 5;
            gameObject.SetActive(true);
            if(_animationSystem != null)
            {
                if(playDefaultAnimation)
                {
                    _animationSystem.PlayAnimation(AnimationPhase.Enter, this.gameObject , () =>
                    {
                        MakeStateInteractable(makeStateinteractable);
                    });   
                }
                else
                {
                    MakeStateInteractable(makeStateinteractable);
                }
            }
            
        }

        public virtual IEnumerator Hide(bool playDefaultAnimation = false)
        {
            if (_isHiding)
                yield break;
            print("UI base hide called");
            _isHiding = true;
            MakeStateInteractable(false);

            bool completed = false;

            if(_animationSystem != null)
            {
                if(playDefaultAnimation)
                {
                    _animationSystem.PlayAnimation(AnimationPhase.Exit, this.gameObject , () =>
                    {
                        completed = true;
                    });
                }
                else
                {
                    completed = true;
                }
            }
            else
            {
                completed = true;
            }

            // FSM WAITS here
            yield return new WaitUntil(() => completed);

            gameObject.SetActive(false);
            _isHiding = false;
            
        }

        private void OnHideComplete()
        {
            gameObject.SetActive(false);
        }

        public virtual void Pause()
        {
            if (!Paused)
            {
                MakeStateInteractable(false);
                Paused = true;
                
                // Complete any ongoing animations before pausing
                _animationSystem.ForceCompleteCurrentAnimation();
            }
        }

        public virtual void Resume()
        {
            if (Paused)
            {
                Paused = false;
                
                // Complete pause animation before resuming
                _animationSystem.ForceCompleteCurrentAnimation();
                MakeStateInteractable(true);
            }
        }

        protected void MakeStateInteractable(bool flag)
        {
            //_canvasGroup.interactable = flag;
            _canvasGroup.blocksRaycasts = flag;
        }
        
        #region UTILITIES

        public virtual void EnableRaycast()
        {
            if (_canvasGroup)
            {
                //_canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
            }
        }
        public virtual void BlockRaycast()
        {
            if (_canvasGroup)
            {
                //_canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }
        }
        
        public IEnumerator Interactable(bool status, float delay = 0)
        {
            yield return new WaitForSeconds(delay);
            if (_canvasGroup != null)
                _canvasGroup.blocksRaycasts = status;
        }
        
        #endregion  
        
        /// <summary>
        /// Synchronous, non-virtual instant close used by the FSM's bulk teardown (ClearAll / multi-pop).
        /// Mirrors the no-animation path of <see cref="Hide"/> (deactivate + block raycasts) WITHOUT touching
        /// alpha — so a pooled instance re-shows correctly — and WITHOUT running any animated Hide override,
        /// guaranteeing occluded states clear in one frame regardless of their view's hide animation.
        /// </summary>
        public void CloseImmediate()
        {
            _isHiding = false;
            MakeStateInteractable(false);
            gameObject.SetActive(false);
        }

        public virtual void HideInstant(Action completeAction = null)
        {
            DOTween.Kill(gameObject);
            DOTween.Kill(_canvasGroup);
            DOTween.Kill(UIPanel);

            if (_canvasGroup)
            {
                _canvasGroup.blocksRaycasts = false;
                //_canvasGroup.interactable = false;
            }
            //transform.localScale = Vector3.one * 0.2f;
            if (_canvasGroup) _canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
            completeAction?.Invoke();
        }

        public void ApplySortingOrder(int startOrder)
        {
            if (childCanvases == null || childCanvases.Count == 0) return;

            int currentOrder = startOrder + 1;
            foreach (var canvas in childCanvases)
            {
                if (canvas == null) continue;
                canvas.sortingOrder = currentOrder++;
            }
        }

        public bool Paused { get; set; }
       
    }
}
