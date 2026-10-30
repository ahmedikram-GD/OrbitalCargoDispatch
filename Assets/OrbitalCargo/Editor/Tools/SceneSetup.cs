using OrbitalCargo.FirstPerson;
using OrbitalCargo.FirstPerson.EditorTools;
using OrbitalCargo.Room.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OrbitalCargo.Shift.EditorTools
{
    /// <summary>
    /// One-click (or batchmode) setup of <c>Game.unity</c> for Slice 1: builds the greybox room, the
    /// Player and Controls prefabs, replaces the template camera with the player's camera, adds an
    /// EventSystem, and wires the mobile input provider to the scene's joystick and look area.
    /// Idempotent — re-running replaces what it placed before.
    /// <para>Batchmode: <c>Unity.exe -batchmode -quit -projectPath . -executeMethod
    /// OrbitalCargo.Shift.EditorTools.SceneSetup.SetupSlice1</c></para>
    /// </summary>
    public static class SceneSetup
    {
        public const string GameScenePath = "Assets/OrbitalCargo/Scenes/Game.unity";

        [MenuItem("Tools/OrbitalCargo/Setup Game Scene (Slice 1)")]
        public static void SetupSlice1()
        {
            var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            var layout = RoomBuilder.LoadOrCreateDefaultLayout();
            RoomBuilder.Build(layout);

            PlayerRigBuilder.BuildPrefabs();

            // Template camera is replaced by the camera inside the Player prefab.
            DestroyIfPresent("Main Camera");
            DestroyIfPresent("Player");
            DestroyIfPresent("Controls");
            DestroyIfPresent("EventSystem");

            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerRigBuilder.PlayerPrefabPath));
            player.transform.SetPositionAndRotation(layout.PlayerSpawn, Quaternion.Euler(0f, layout.PlayerYaw, 0f));

            var controls = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerRigBuilder.ControlsPrefabPath));
            PlayerRigBuilder.CreateEventSystem();

            // Scene-level wiring: the two prefabs are independent, so the link lives on the instance.
            var input = new SerializedObject(player.GetComponent<MobileMoveInputProvider>());
            input.FindProperty("joystick").objectReferenceValue = controls.GetComponentInChildren<VirtualJoystick>(true);
            input.FindProperty("lookArea").objectReferenceValue = controls.GetComponentInChildren<TouchLookArea>(true);
            input.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneSetup] Game.unity set up for Slice 1 (room, player, controls, event system).");
        }

        private static void DestroyIfPresent(string name)
        {
            var go = GameObject.Find(name);
            if (go != null) Object.DestroyImmediate(go);
        }
    }
}
