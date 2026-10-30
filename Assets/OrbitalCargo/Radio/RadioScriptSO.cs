using System;
using System.Collections.Generic;
using UnityEngine;

namespace OrbitalCargo.Radio
{
    /// <summary>One radio line, keyed by the beat that triggers it.</summary>
    [Serializable]
    public class RadioLine
    {
        [Tooltip("Beat key raised on e_RadioBeat (schedule entries, RookRadio): Intro, FirstPlace, FirstLaunch, CryoIntro, Suspicious, Junk, Volatile, ShiftEnd, ShiftStart.")]
        public string Beat;
        public string Speaker = "ROOK";
        [TextArea(2, 4)] public string Text;
        [Tooltip("Seconds the line stays up after it has finished typing.")]
        [Min(0.5f)] public float HoldSeconds = 4f;
    }

    /// <summary>
    /// The radio script: what each beat says. Data only — <see cref="RookRadio"/> decides when beats
    /// happen and <c>RadioLineView</c> shows them. A beat with no line is silently ignored, so writers can
    /// add or cut lines without touching code.
    /// </summary>
    [CreateAssetMenu(fileName = "RadioScript_", menuName = "OrbitalCargo/Radio/Radio Script")]
    public class RadioScriptSO : ScriptableObject
    {
        public List<RadioLine> Lines = new List<RadioLine>();

        public bool TryGet(string beat, out RadioLine line)
        {
            line = null;
            if (string.IsNullOrEmpty(beat) || Lines == null) return false;
            foreach (var candidate in Lines)
            {
                if (candidate == null || candidate.Beat != beat) continue;
                line = candidate;
                return true;
            }
            return false;
        }
    }
}
