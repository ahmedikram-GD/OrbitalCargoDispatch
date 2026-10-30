using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OrbitalCargo.Juice
{
    /// <summary>
    /// Plays camera shakes on the camera it sits on, applied ONLY while the camera renders:
    /// <c>beginCameraRendering</c> adds the offset and <c>endCameraRendering</c> restores the exact pose,
    /// so look, head-bob and anything else driving the camera never see or fight the shake. Up to
    /// <see cref="MaxShakes"/> shakes add together. Real time throughout. Ported from Silly-Justice
    /// (its DataManager channel replaced by a direct <see cref="Play"/> call from the juice presenter).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("OrbitalCargo/Juice/Camera Shaker")]
    public sealed class CameraShaker : MonoBehaviour
    {
        private const int MaxShakes = 4;

        [Tooltip("Scales every shake. 0 = off, 1 = as authored. A settings toggle can drive this.")]
        [Range(0f, 2f)] [SerializeField] private float intensity = 1f;

        private struct ActiveShake
        {
            public CameraShakeProfileSO Profile;
            public float StartTime;
            public float Seed;
        }

        private readonly List<ActiveShake> _active = new List<ActiveShake>(MaxShakes);

        private Camera _camera;
        private bool _applied;
        private Vector3 _savedLocalPosition;
        private Quaternion _savedLocalRotation;

        public float Intensity
        {
            get => intensity;
            set => intensity = Mathf.Clamp(value, 0f, 2f);
        }

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake() => _camera = GetComponent<Camera>();

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += HandleBeginCamera;
            RenderPipelineManager.endCameraRendering += HandleEndCamera;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= HandleBeginCamera;
            RenderPipelineManager.endCameraRendering -= HandleEndCamera;
            Restore();
            _active.Clear();
        }

        // ── Public API ─────────────────────────────────────────────────────────

        public void Play(CameraShakeProfileSO profile)
        {
            if (profile == null || profile.Duration <= 0f) return;

            float now = Time.unscaledTime;
            if (_active.Count >= MaxShakes) _active.RemoveAt(IndexNearestItsEnd(now));
            _active.Add(new ActiveShake { Profile = profile, StartTime = now, Seed = Random.Range(0f, 1000f) });
        }

        // ── Handlers ───────────────────────────────────────────────────────────

        private void HandleBeginCamera(ScriptableRenderContext context, Camera cam)
        {
            if (cam != _camera) return;
            Restore(); // a render that never ended must not leave its offset behind
            if (_active.Count == 0 || intensity <= 0f) return;

            float now = Time.unscaledTime;
            Vector3 position = Vector3.zero;
            Quaternion rotation = Quaternion.identity;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var shake = _active[i];
                if (shake.Profile == null ||
                    !shake.Profile.Sample(now - shake.StartTime, shake.Seed, out Vector3 p, out Quaternion r))
                {
                    _active.RemoveAt(i);
                    continue;
                }
                position += p;
                rotation *= r;
            }
            if (_active.Count == 0) return;

            var t = transform;
            _savedLocalPosition = t.localPosition;
            _savedLocalRotation = t.localRotation;
            _applied = true;

            t.localPosition = _savedLocalPosition + _savedLocalRotation * (position * intensity);
            t.localRotation = _savedLocalRotation * Quaternion.SlerpUnclamped(Quaternion.identity, rotation, intensity);
        }

        private void HandleEndCamera(ScriptableRenderContext context, Camera cam)
        {
            if (cam == _camera) Restore();
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void Restore()
        {
            if (!_applied) return;
            _applied = false;
            transform.localPosition = _savedLocalPosition;
            transform.localRotation = _savedLocalRotation;
        }

        private int IndexNearestItsEnd(float now)
        {
            int best = 0;
            float bestLeft = float.MaxValue;
            for (int i = 0; i < _active.Count; i++)
            {
                var s = _active[i];
                float left = s.Profile != null ? s.Profile.Duration - (now - s.StartTime) : float.MinValue;
                if (left < bestLeft) { bestLeft = left; best = i; }
            }
            return best;
        }
    }
}
