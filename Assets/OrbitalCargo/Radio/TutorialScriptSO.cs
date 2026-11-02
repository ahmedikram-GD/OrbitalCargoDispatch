using System;
using System.Collections.Generic;
using UnityEngine;

namespace OrbitalCargo.Radio
{
    /// <summary>A centred instruction card for the control lessons (move, look).</summary>
    [Serializable]
    public class TutorialCard
    {
        public string Title = "MOVE AROUND";
        [Tooltip("Shown on touch devices. TMP rich text is fine (<color=#9CFF57>left thumb</color>).")]
        [TextArea(2, 3)] public string Body;
        [Tooltip("Shown when there's no touchscreen (Editor): name the keys / mouse instead of thumbs.")]
        [TextArea(2, 3)] public string KeyboardBody;
    }

    /// <summary>
    /// The one-time guided delivery for a crate kind: what the objective card says while the crate needs
    /// fetching and while it's being carried to where it belongs. Bay names in the text should be tinted
    /// the bay's colour — that pairing (crate colour ↔ pad colour) is the lesson.
    /// </summary>
    [Serializable]
    public class CrateLesson
    {
        [Tooltip("CrateTypeSO.Id (standard, cryo, junk, volatile), or 'suspicious' for a suspicious crate of any type.")]
        public string Key;
        [Tooltip("Objective card heading, e.g. CRYO CRATE.")]
        public string Title;
        [Tooltip("While the crate is on the belt / lying around: go get it.")]
        [TextArea(2, 3)] public string FetchText;
        [Tooltip("While carrying it: where it goes and why.")]
        [TextArea(2, 3)] public string DeliverText;
        [Tooltip("Suspicious only: after a clean scan.")]
        [TextArea(2, 3)] public string CleanText;
        [Tooltip("Suspicious only: after the scan finds contraband.")]
        [TextArea(2, 3)] public string BadText;
        [Tooltip("Show the swipe-up hand while carrying it (junk: swipe to throw).")]
        public bool ShowThrowGesture;
    }

    /// <summary>
    /// Everything the first-shift onboarding says and how it paces itself: the move/look cards and how
    /// much moving/looking clears them, the camera nudge toward each new target, guide-arrow heights per
    /// station, one <see cref="CrateLesson"/> per crate kind and the "hit LAUNCH" lesson. Data only —
    /// <see cref="TutorialDirector"/> runs it; writers can retune or reword without touching code.
    /// </summary>
    [CreateAssetMenu(fileName = "TutorialScript_", menuName = "OrbitalCargo/Radio/Tutorial Script")]
    public class TutorialScriptSO : ScriptableObject
    {
        public const string SuspiciousKey = "suspicious";

        [Header("Control lessons (the shift waits for these)")]
        public TutorialCard MoveCard = new TutorialCard();
        [Tooltip("Metres walked to clear the move card.")]
        [Min(0.1f)] public float MoveDistance = 2.5f;
        public TutorialCard LookCard = new TutorialCard { Title = "LOOK AROUND" };
        [Tooltip("Degrees turned (yaw + pitch, any direction) to clear the look card.")]
        [Min(1f)] public float LookDegrees = 120f;
        [Tooltip("Seconds the cleared card lingers (with its tick) before the next one.")]
        [Min(0f)] public float CardClearDelay = 0.6f;

        [Header("Camera nudge (auto-aim at each new target)")]
        [Tooltip("Seconds the view is steered toward a new target; the player's look is ignored meanwhile. 0 = off.")]
        [Min(0f)] public float AimSeconds = 0.9f;
        [Tooltip("How quickly the view converges on the target (higher = snappier).")]
        [Min(0.1f)] public float AimSharpness = 5f;

        [Header("Guide arrow height above the target (m)")]
        [Min(0f)] public float ArrowAboveCrate = 1.3f;
        [Min(0f)] public float ArrowAboveBay = 4.3f;
        [Min(0f)] public float ArrowAboveChute = 1.4f;
        [Min(0f)] public float ArrowAboveScanner = 3.6f;
        [Min(0f)] public float ArrowAboveLaunchButton = 2f;

        [Header("Lessons")]
        public List<CrateLesson> CrateLessons = new List<CrateLesson>();
        [Tooltip("Shown the first time a bay fills up: the arrow moves to that bay's launch button.")]
        public CrateLesson LaunchLesson = new CrateLesson { Key = "launch", Title = "BAY FULL" };
        [Tooltip("Objective swatch colour for the launch lesson.")]
        public Color LaunchColor = new Color(0.2f, 1f, 0.4f);

        public bool TryGetLesson(string key, out CrateLesson lesson)
        {
            lesson = null;
            if (string.IsNullOrEmpty(key) || CrateLessons == null) return false;
            foreach (var candidate in CrateLessons)
            {
                if (candidate == null || candidate.Key != key) continue;
                lesson = candidate;
                return true;
            }
            return false;
        }
    }
}
