using DG.Tweening;
using UnityEngine;

namespace OrbitalCargo.Radio
{
    /// <summary>
    /// Bobbing, spinning marker that hangs over whatever the rookie should go to next — a crate on the
    /// belt, its bay, the chute, the scanner, a launch button. Moved, shown and hidden by
    /// <see cref="TutorialDirector"/>; pure presentation.
    /// </summary>
    [AddComponentMenu("OrbitalCargo/Radio/Guide Arrow")]
    public sealed class GuideArrow : MonoBehaviour
    {
        [Tooltip("The geometry that bobs and spins (child), so this object's own pose stays the anchor.")]
        [SerializeField] private Transform visual;
        [Min(0f)] [SerializeField] private float bobHeight = 0.25f;
        [Min(0.1f)] [SerializeField] private float bobPeriod = 0.8f;
        [Tooltip("Degrees per second.")]
        [SerializeField] private float spinSpeed = 90f;

        private Tween _bob;

        public bool IsShown => gameObject.activeSelf;

        private void OnEnable()
        {
            if (visual == null) return;
            visual.localPosition = Vector3.zero;
            _bob = visual.DOLocalMoveY(-bobHeight, bobPeriod * 0.5f)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetLink(gameObject);
        }

        private void OnDisable()
        {
            _bob?.Kill();
            _bob = null;
        }

        private void Update()
        {
            if (visual != null) visual.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        }

        public void Show() => gameObject.SetActive(true);

        public void Hide() => gameObject.SetActive(false);

        /// <summary>Hang the arrow at <paramref name="worldPosition"/> (the tip points down at it) and show it.</summary>
        public void ShowAt(Vector3 worldPosition)
        {
            transform.position = worldPosition;
            if (!IsShown) Show();
        }
    }
}
