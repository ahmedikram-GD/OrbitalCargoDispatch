using System;
using System.Collections;
using UnityEngine;

namespace OrbitalCargo.Crates
{
    /// <summary>
    /// Moves a crate between owners without physics: flies it to a holder (the player's hold point or
    /// a bay slot) along a small arc, then parents it there. The target is re-read every frame, so a
    /// crate flying to the hand follows the walking player. Trimmed from Silly-Justice's
    /// <c>GrabbableItem</c> (attach lerp + parent, scale pop) with the web-strand FX, respawn and
    /// physics release removed. Colliders are off while carried so the pick ray sees past the crate.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("OrbitalCargo/Crates/Carryable Crate")]
    public sealed class CarryableCrate : MonoBehaviour
    {
        private Rigidbody _body;
        private Collider[] _colliders;
        private Coroutine _move;
        private Vector3 _baseScale;

        // The in-flight move's destination, so CompleteMove can land it instantly.
        private Transform _moveHolder;
        private Vector3 _moveOffset;
        private Vector3 _moveScale;
        private Action _moveDone;

        /// <summary>True while a fly/snap move is in progress.</summary>
        public bool IsMoving => _move != null;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _body.isKinematic = true;
            _body.useGravity = false;
            _colliders = GetComponentsInChildren<Collider>(true);
            _baseScale = transform.localScale;
        }

        private void OnDisable() => StopMove();

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>Fly into the hand: colliders off, shrink to <paramref name="scale"/>, parent at <paramref name="localOffset"/>.</summary>
        public void AttachTo(Transform holder, Vector3 localOffset, float scale, float duration, float arcHeight, Action onAttached = null)
        {
            SetCollidersEnabled(false);
            StartMove(holder, localOffset, _baseScale * scale, duration, arcHeight, onAttached);
        }

        /// <summary>Fly onto a slot anchor at full size, then re-enable colliders so it can be aimed at.</summary>
        public void SnapTo(Transform anchor, float duration, float arcHeight, Action onSnapped = null)
        {
            StartMove(anchor, Vector3.zero, _baseScale, duration, arcHeight, () =>
            {
                SetCollidersEnabled(true);
                onSnapped?.Invoke();
            });
        }

        /// <summary>Unparent in place (keeps the current pose). Used before handing the crate to a new owner.</summary>
        public void Detach()
        {
            StopMove();
            transform.SetParent(null, true);
        }

        /// <summary>
        /// Land an in-flight move right now (final pose + its completion callback). Call before another
        /// system takes the crate — e.g. a launch the same moment the last crate is still snapping in,
        /// whose snap would otherwise finish later by re-parenting the crate off the departing shuttle.
        /// </summary>
        public void CompleteMove()
        {
            if (_move == null) return;
            StopCoroutine(_move);
            _move = null;
            Land(_moveHolder, _moveOffset, _moveScale, _moveDone);
        }

        /// <summary>Put the crate straight onto <paramref name="anchor"/> at full size with colliders on (pool reuse, editor placement).</summary>
        public void PlaceImmediate(Transform anchor)
        {
            StopMove();
            transform.SetParent(anchor, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = _baseScale;
            SetCollidersEnabled(true);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void StartMove(Transform holder, Vector3 localOffset, Vector3 targetScale, float duration, float arcHeight, Action onDone)
        {
            StopMove();
            transform.SetParent(null, true);
            _moveHolder = holder;
            _moveOffset = localOffset;
            _moveScale = targetScale;
            _moveDone = onDone;
            _move = StartCoroutine(MoveRoutine(holder, localOffset, targetScale, duration, arcHeight, onDone));
        }

        private void StopMove()
        {
            if (_move == null) return;
            StopCoroutine(_move);
            _move = null;
        }

        private IEnumerator MoveRoutine(Transform holder, Vector3 localOffset, Vector3 targetScale, float duration, float arcHeight, Action onDone)
        {
            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;
            Vector3 startScale = transform.localScale;

            for (float t = 0f; t < 1f && duration > 0f; t += Time.deltaTime / duration)
            {
                if (holder == null) break;
                float s = Mathf.SmoothStep(0f, 1f, t);
                Vector3 target = holder.TransformPoint(localOffset);
                transform.position = Vector3.Lerp(startPos, target, s) + Vector3.up * (arcHeight * Mathf.Sin(Mathf.PI * t));
                transform.rotation = Quaternion.Slerp(startRot, holder.rotation, s);
                transform.localScale = Vector3.Lerp(startScale, targetScale, s);
                yield return null;
            }

            _move = null;
            Land(holder, localOffset, targetScale, onDone);
        }

        private void Land(Transform holder, Vector3 localOffset, Vector3 targetScale, Action onDone)
        {
            if (holder != null)
            {
                transform.SetParent(holder, false);
                transform.localPosition = localOffset;
                transform.localRotation = Quaternion.identity;
                transform.localScale = targetScale;
            }
            onDone?.Invoke();
        }

        private void SetCollidersEnabled(bool on)
        {
            if (_colliders == null) _colliders = GetComponentsInChildren<Collider>(true);
            foreach (var c in _colliders)
                if (c != null) c.enabled = on;
        }
    }
}
