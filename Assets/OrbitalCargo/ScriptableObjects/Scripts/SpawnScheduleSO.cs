using System;
using System.Collections.Generic;
using UnityEngine;

namespace OrbitalCargo.Config
{
    /// <summary>
    /// The scripted spawn timeline of a shift: when which crates arrive, how many, how far apart,
    /// whether they're suspicious, and which radio beat (Rook's line) fires with the first of them.
    /// Expanded into individual spawns by <c>SpawnScheduler</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "SpawnSchedule_", menuName = "OrbitalCargo/Shift/Spawn Schedule")]
    public class SpawnScheduleSO : ScriptableObject
    {
        public List<SpawnEntry> Entries = new List<SpawnEntry>();
    }

    [Serializable]
    public class SpawnEntry
    {
        [Tooltip("Shift time (s) of the first crate of this entry.")]
        [Min(0f)] public float Time;
        public CrateTypeSO Type;
        [Min(1)] public int Count = 1;
        [Tooltip("Seconds between crates when Count > 1.")]
        [Min(0f)] public float Interval = 2f;
        [Tooltip("Crate shows a tell (shake/sparks/drip) and should be scanned.")]
        public bool Suspicious;
        [Tooltip("What's inside (null = clean, a false alarm if Suspicious).")]
        public HiddenItemSO Hidden;
        [Tooltip("Radio beat raised with the FIRST crate of this entry (e.g. 'Intro', 'CryoIntro'). Empty = none.")]
        public string RadioBeat;
    }

    /// <summary>One crate to spawn, expanded from a <see cref="SpawnEntry"/>.</summary>
    public struct SpawnRequest
    {
        public float Time;
        public CrateTypeSO Type;
        public bool Suspicious;
        public HiddenItemSO Hidden;
        public string RadioBeat;
    }
}
