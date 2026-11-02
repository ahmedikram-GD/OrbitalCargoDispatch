using System.Collections.Generic;
using System.IO;
using OrbitalCargo.Room.EditorTools;
using UnityEditor;
using UnityEngine;

namespace OrbitalCargo.Radio.EditorTools
{
    /// <summary>
    /// Creates <c>RadioScript_Pitch</c> with Rook's tutorial lines and <c>TutorialScript_Pitch</c> with the
    /// onboarding lessons (each created once, then the writer owns it), and builds the greybox guide
    /// arrow for the scene. Beat keys match the spawn schedule's RadioBeat entries and the constants on
    /// <see cref="RookRadio"/>; lesson keys match <c>CrateTypeSO.Id</c>.
    /// </summary>
    public static class RadioAssetFactory
    {
        public const string ScriptPath = "Assets/OrbitalCargo/ScriptableObjects/Config/RadioScript_Pitch.asset";
        public const string TutorialScriptPath = "Assets/OrbitalCargo/ScriptableObjects/Config/TutorialScript_Pitch.asset";

        [MenuItem("Tools/OrbitalCargo/Create Radio Script")]
        public static RadioScriptSO EnsureScript()
        {
            var existing = AssetDatabase.LoadAssetAtPath<RadioScriptSO>(ScriptPath);
            if (existing != null) return existing;

            var script = ScriptableObject.CreateInstance<RadioScriptSO>();
            script.Lines = new List<RadioLine>
            {
                Line(RookRadio.IntroBeat, "Evening, rookie. Grey crate on the belt — grab it and walk it to Bay A, the grey one.", 5f),
                Line(RookRadio.FirstPlaceBeat, "Slot's lit. Fill all six and hit the big green button.", 4f),
                Line(RookRadio.FirstLaunchBeat, "There she goes. Credits in your account — don't spend it all.", 4f),
                Line("CryoIntro", "White crates are cryo. Bay B, and move — that ring runs out.", 4.5f),
                Line("Suspicious", "That one looks off… take it to the booth and scan it.", 4.5f),
                Line("Junk", "Rusty junk goes down the chute. Swipe it in.", 4f),
                Line("Volatile", "Yellow chevrons: volatile. Bay C, slow and steady.", 4f),
                Line(RookRadio.WrongBayBeat, "Wrong pad, rookie — crate colour matches the bay. That one ships at half pay.", 4.5f),
                Line(RookRadio.ShiftStartBeat, "Back for more? Belt's running hot tonight — keep up.", 4f),
                Line(RookRadio.ShiftEndBeat, "Shift's over. Not bad for a first night.", 5f),
            };
            Directory.CreateDirectory(Path.GetDirectoryName(ScriptPath));
            AssetDatabase.CreateAsset(script, ScriptPath);
            return script;
        }

        /// <summary>
        /// Creates <c>TutorialScript_Pitch</c> (once — then the writer owns it): the move/look cards and one
        /// guided delivery per crate kind. Bay names are tinted the pad colours from RoomLayout_Default
        /// (brightened to read on the dark card) so the crate colour ↔ bay colour pairing is the lesson.
        /// </summary>
        [MenuItem("Tools/OrbitalCargo/Create Tutorial Script")]
        public static TutorialScriptSO EnsureTutorialScript()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TutorialScriptSO>(TutorialScriptPath);
            if (existing != null) return existing;

            const string bayA = "<color=#9DB6D9><b>BAY A</b></color>";
            const string bayB = "<color=#CCF2FF><b>BAY B</b></color>";
            const string bayC = "<color=#FFCC1A><b>BAY C</b></color>";
            const string chute = "<color=#FF7A3D><b>CHUTE</b></color>";
            const string booth = "<color=#7CFF9B><b>SCANNER</b></color>";

            var script = ScriptableObject.CreateInstance<TutorialScriptSO>();
            script.MoveCard = new TutorialCard
            {
                Title = "MOVE AROUND",
                Body = "Use your <color=#9CFF57>left thumb</color> to move around.",
                KeyboardBody = "Use <color=#9CFF57>W A S D</color> to move around.",
            };
            script.LookCard = new TutorialCard
            {
                Title = "LOOK AROUND",
                Body = "Drag your <color=#9CFF57>right thumb</color> to look around.",
                KeyboardBody = "Drag with the <color=#9CFF57>mouse</color> to look around.",
            };
            script.CrateLessons = new List<CrateLesson>
            {
                new CrateLesson
                {
                    Key = "standard", Title = "GREY CRATE",
                    FetchText = "Grey crate on the belt — walk up and grab it.",
                    DeliverText = $"Grey crates go to {bayA} — the grey pad. Match the colours!",
                },
                new CrateLesson
                {
                    Key = "cryo", Title = "CRYO CRATE",
                    FetchText = "White crate with a blue ring — it's thawing. Grab it, quick!",
                    DeliverText = $"White crates go to {bayB} — before the ring runs out.",
                },
                new CrateLesson
                {
                    Key = "junk", Title = "JUNK",
                    FetchText = "Rusty junk — nobody pays to ship that. Grab it.",
                    DeliverText = $"Swipe up (Space) to throw it down the {chute}.",
                    ShowThrowGesture = true,
                },
                new CrateLesson
                {
                    Key = "volatile", Title = "VOLATILE CRATE",
                    FetchText = "Yellow chevrons — volatile. Pick it up gently.",
                    DeliverText = $"Yellow crates go to {bayC}. You'll walk slower — steady!",
                },
                new CrateLesson
                {
                    Key = TutorialScriptSO.SuspiciousKey, Title = "SUSPICIOUS CRATE",
                    FetchText = "That crate is shaking and dripping goo… grab it.",
                    DeliverText = $"Take it to the {booth} booth before it ships.",
                    CleanText = "Clean! Grab it and ship it to its colour's bay.",
                    BadText = $"Contraband! Grab it and dump it down the {chute}.",
                },
            };
            script.LaunchLesson = new CrateLesson
            {
                Key = "launch", Title = "BAY FULL",
                DeliverText = "Hit the green <color=#7CFF9B><b>LAUNCH</b></color> button by the pad!",
            };

            Directory.CreateDirectory(Path.GetDirectoryName(TutorialScriptPath));
            AssetDatabase.CreateAsset(script, TutorialScriptPath);
            return script;
        }

        /// <summary>A green down-pointing chevron on a short stem, hidden until the tutorial needs it.</summary>
        public static GuideArrow BuildArrow(Transform parent, Vector3 worldPosition)
        {
            var root = new GameObject("GuideArrow");
            root.transform.SetParent(parent, false);
            root.transform.position = worldPosition;

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            var material = GreyboxMaterials.Get("GuideArrow", new Color(0.2f, 1f, 0.45f), 2f);

            Part(visual, "Stem", new Vector3(0f, 0.45f, 0f), Vector3.zero, new Vector3(0.14f, 0.6f, 0.14f), material);
            Part(visual, "Left", new Vector3(-0.17f, 0.06f, 0f), new Vector3(0f, 0f, 45f), new Vector3(0.14f, 0.5f, 0.14f), material);
            Part(visual, "Right", new Vector3(0.17f, 0.06f, 0f), new Vector3(0f, 0f, -45f), new Vector3(0.14f, 0.5f, 0.14f), material);

            var arrow = root.AddComponent<GuideArrow>();
            var so = new SerializedObject(arrow);
            so.FindProperty("visual").objectReferenceValue = visual;
            so.ApplyModifiedPropertiesWithoutUndo();
            return arrow;
        }

        private static RadioLine Line(string beat, string text, float hold) =>
            new RadioLine { Beat = beat, Speaker = "ROOK", Text = text, HoldSeconds = hold };

        private static void Part(Transform parent, string name, Vector3 pos, Vector3 euler, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = pos;
            part.transform.localRotation = Quaternion.Euler(euler);
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(part.GetComponent<Collider>()); // never blocks the crosshair ray
        }
    }
}
