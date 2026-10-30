using GameKit.UI;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalCargo.UI
{
    /// <summary>Pause panel (pooled "PausePanel"). Passive: <c>PauseState</c> binds the resume button.</summary>
    [AddComponentMenu("OrbitalCargo/UI/Pause View")]
    public sealed class PauseView : UIBase
    {
        [SerializeField] private Button resumeButton;

        public Button ResumeButton => resumeButton;
    }
}
