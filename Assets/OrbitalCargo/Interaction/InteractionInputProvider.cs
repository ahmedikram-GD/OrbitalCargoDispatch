using UnityEngine;
using UnityEngine.InputSystem;

namespace OrbitalCargo.Interaction
{
    /// <summary>
    /// Default <see cref="IInteractInput"/>. Latches a request from the keyboard (E = interact,
    /// Space = throw) or from <see cref="RequestInteract"/>/<see cref="RequestThrow"/>, which the
    /// context button and the junk swipe call. Cleared only when consumed, so no request is lost
    /// regardless of update order. Ported from Silly-Justice (E/Q there; Space is our throw key).
    /// </summary>
    [AddComponentMenu("OrbitalCargo/Interaction/Interaction Input Provider")]
    public sealed class InteractionInputProvider : MonoBehaviour, IInteractInput
    {
        private bool _interact;
        private bool _throw;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.eKey.wasPressedThisFrame) _interact = true;
            if (kb.spaceKey.wasPressedThisFrame) _throw = true;
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>Hook for the context button.</summary>
        public void RequestInteract() => _interact = true;

        /// <summary>Hook for the junk swipe.</summary>
        public void RequestThrow() => _throw = true;

        public bool ConsumeInteract()
        {
            if (!_interact) return false;
            _interact = false;
            return true;
        }

        public bool ConsumeThrow()
        {
            if (!_throw) return false;
            _throw = false;
            return true;
        }
    }
}
