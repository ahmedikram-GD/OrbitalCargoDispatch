using UnityEngine;
using System.Collections;
using GameKit.UI;
using Sirenix.OdinInspector;
using GameKit.Architecture.PoolSystem;
using GameKit.StateMachine.Application;

namespace GameKit.StateMachine
{
    public class UIViewState : State
    {
        [SerializeField] protected string stateId;

        [SerializeField] protected bool usePooling = true;

        [ShowIf("@usePooling")]
        [SerializeField, Required]
        [InfoBox("Ensure prefab is registered in PoolManagerSO.", InfoMessageType.None)]
        private PoolManagerSO uIStatesPooler;

        protected UIBase _uiInstance;
        private GameObject _spawnedInstance;

        public override IEnumerator Enter(IState previous)
        {
            yield return base.Enter(previous);

            GameObject viewObject = null;

            if (usePooling)
            {
                viewObject = uIStatesPooler.Get(stateId);
                if (viewObject == null)
                {
                    Debug.LogError($"[UIViewState] No pooled GameObject found for stateId: {stateId}");
                    yield break;
                }
                viewObject.transform.SetParent(StateRootManager.UIStates);
                viewObject.transform.SetAsLastSibling();
                SetViewTranform(viewObject);
            }
            else
            {
                var prefab = Resources.Load<GameObject>(stateId);
                if (prefab == null)
                {
                    Debug.LogError($"[UIViewState] Prefab not found in Resources at path: {stateId}");
                    yield break;
                }

                viewObject = Instantiate(prefab);
                viewObject.transform.SetParent(StateRootManager.UIStates);
                viewObject.transform.SetAsLastSibling();
                SetViewTranform(viewObject);
                _spawnedInstance = viewObject;
            }

            _uiInstance = viewObject.GetComponent<UIBase>();
            if (_uiInstance == null)
            {
                Debug.LogError($"[UIViewState] GameObject at '{stateId}' does not contain UiBase component.");
                yield break;
            }

            viewObject.SetActive(true);
            _uiInstance.Show();

            // The STATE is the brain: it subscribes to the view's buttons here.
            // The view never references the state.
            OnBindView(_uiInstance);
        }

        public override IEnumerator Exit()
        {
            if (_uiInstance != null)
            {
                var instance = _uiInstance;
                _uiInstance = null;          // null first — prevents double-release if Exit is called again mid-coroutine

                // Block interaction immediately so rapid clicks during the
                // transition gap (before Hide's animation starts) are ignored.
                instance.BlockRaycast();

                // Unsubscribe the state's button callbacks before the view leaves.
                OnUnbindView(instance);

                yield return instance.Hide();

                if (usePooling)
                {
                    uIStatesPooler.Release(stateId, instance.gameObject);
                }
                else if (_spawnedInstance != null)
                {
                    Destroy(_spawnedInstance);
                    _spawnedInstance = null;
                }
            }

            yield return base.Exit();
        }

        // Instant teardown for bulk clears — close the view in one frame with no exit animation and no
        // await (it's occluded). Mirrors Exit()'s release/pool logic but skips the animated Hide().
        public override IEnumerator ExitImmediate()
        {
            if (_uiInstance != null)
            {
                var instance = _uiInstance;
                _uiInstance = null;

                OnUnbindView(instance);
                instance.CloseImmediate();

                if (usePooling)
                {
                    uIStatesPooler.Release(stateId, instance.gameObject);
                }
                else if (_spawnedInstance != null)
                {
                    Destroy(_spawnedInstance);
                    _spawnedInstance = null;
                }
            }

            yield break;
        }


        public override IEnumerator Pause()
        {
            _uiInstance?.Pause();
            yield return base.Pause();
        }

        public override IEnumerator Resume()
        {
            _uiInstance?.Resume();
            yield return base.Resume();
        }

        // ── State ⇄ View binding (MVP) ──────────────────────────────────────────
        // The STATE is the brain. Override these to subscribe / unsubscribe the
        // view's button callbacks — e.g. view.CloseButton.onClick.AddListener(Close).
        // The VIEW must never reference the state.

        /// <summary>Called after the view is shown. Subscribe to view button callbacks here.</summary>
        protected virtual void OnBindView(UIBase view) { }

        /// <summary>Called before the view is hidden. Remove the listeners added in OnBindView.</summary>
        protected virtual void OnUnbindView(UIBase view) { }

        private void SetViewTranform(GameObject view)
        {
            RectTransform rt = view.GetComponent<RectTransform>();

            rt.anchorMin = Vector2.zero;      // (0,0)
            rt.anchorMax = Vector2.one;       // (1,1)

            rt.offsetMin = Vector2.zero;      // Left & Bottom = 0
            rt.offsetMax = Vector2.zero;   
               // Right & Top = 0
            rt.anchoredPosition3D = Vector3.zero;

            view.transform.localScale = Vector3.one;
            view.transform.localRotation = Quaternion.identity;
            
        }
    
    //
    }
}
