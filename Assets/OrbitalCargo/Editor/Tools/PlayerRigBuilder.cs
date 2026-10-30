using System.IO;
using OrbitalCargo.Crates.EditorTools;
using OrbitalCargo.FirstPerson;
using OrbitalCargo.Interaction;
using OrbitalCargo.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace OrbitalCargo.FirstPerson.EditorTools
{
    /// <summary>
    /// Builds the <c>Player</c> and <c>Controls</c> prefabs from the ported first-person scripts, so
    /// the rig is reproducible and every reference is wired by code rather than by hand.
    /// <para>Player: root (CharacterController + motor + look + controller + mobile input) → Main Camera
    /// (camera bob) → HoldPoint. Controls: overlay canvas 1920×1080 with, in raycast order, the
    /// full-screen TouchLookArea, the floating joystick (left touch zone) and the crosshair. The
    /// context button arrives in Slice 2.</para>
    /// </summary>
    public static class PlayerRigBuilder
    {
        public const string PlayerPrefabPath = "Assets/OrbitalCargo/Prefabs/Player/Player.prefab";
        public const string ControlsPrefabPath = "Assets/OrbitalCargo/Prefabs/UI/Controls.prefab";

        private const string ConfigFolder = "Assets/OrbitalCargo/ScriptableObjects/Config";

        [MenuItem("Tools/OrbitalCargo/Build Player And Controls Prefabs")]
        public static void BuildPrefabs()
        {
            BuildPlayerPrefab();
            BuildControlsPrefab();
            AssetDatabase.SaveAssets();
        }

        // ── Player ─────────────────────────────────────────────────────────────

        public static GameObject BuildPlayerPrefab()
        {
            var root = new GameObject("Player");
            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.3f;

            var motor = root.AddComponent<CharacterMotor>();
            var look = root.AddComponent<FirstPersonLook>();
            root.AddComponent<FirstPersonController>();
            root.AddComponent<MobileMoveInputProvider>();

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(root.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.fieldOfView = 70f;
            camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            camGo.AddComponent<AudioListener>();
            var bob = camGo.AddComponent<CameraBobController>();
            camGo.AddComponent<Juice.CameraShaker>();

            // Crate pivot is its bottom centre, so this is where the bottom of a carried crate sits:
            // low and to the right, clear of the crosshair.
            var hold = new GameObject("HoldPoint").transform;
            hold.SetParent(camGo.transform, false);
            hold.localPosition = new Vector3(0.3f, -0.55f, 0.85f);

            Assign(motor, "config", LoadConfig<MovementConfigSO>("FP_MovementConfig"));
            Assign(look, "config", LoadConfig<LookConfigSO>("FP_LookConfig"));
            Assign(look, "cameraPivot", camGo.transform);
            Assign(bob, "config", LoadConfig<CameraBobConfigSO>("FP_CameraBobConfig"));

            // Interaction (Slice 2): input latch, hands, centre-ray brain.
            var interactionConfig = LoadConfig<InteractionConfigSO>("FP_InteractionConfig");
            root.AddComponent<InteractionInputProvider>();
            var carrier = root.AddComponent<CrateCarrier>();
            var interactor = root.AddComponent<CrateInteractor>();
            Assign(carrier, "config", interactionConfig);
            Assign(carrier, "holdPoint", hold);
            Assign(interactor, "config", interactionConfig);
            Assign(interactor, "aim", camGo.transform);
            Assign(interactor, "carrier", carrier);
            Assign(interactor, "onCratePicked", CrateAssetFactory.EnsureCrateEvent("e_CratePicked"));

            // Junk throw (Slice 4). Look area and chute are scene objects, wired by SceneSetup.
            var thrower = root.AddComponent<JunkThrower>();
            Assign(thrower, "carrier", carrier);
            Assign(thrower, "config", interactionConfig);
            Assign(thrower, "aim", camGo.transform);
            Assign(thrower, "onCrateThrown", CrateAssetFactory.EnsureCrateEvent("e_CrateThrown"));

            return SavePrefab(root, PlayerPrefabPath);
        }

        // ── Controls ───────────────────────────────────────────────────────────

        public static GameObject BuildControlsPrefab()
        {
            var root = new GameObject("Controls", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();
            root.AddComponent<CanvasGroup>();

            // 1) Look area first so every later sibling wins the raycast.
            var lookRt = Rect("TouchLookArea", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Invisible(lookRt);
            lookRt.gameObject.AddComponent<TouchLookArea>();

            // 2) Joystick touch zone: bottom-left 40% × 70% of the screen; the stick floats under the finger.
            var zone = Rect("Joystick", root.transform, Vector2.zero, new Vector2(0.4f, 0.7f), Vector2.zero, Vector2.zero);
            Invisible(zone);
            var joystick = zone.gameObject.AddComponent<VirtualJoystick>();

            var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            var bg = Rect("Background", zone, Vector2.zero, Vector2.zero, new Vector2(280f, 280f), new Vector2(260f, 240f));
            var bgImage = bg.gameObject.AddComponent<Image>();
            bgImage.sprite = knob;
            bgImage.color = new Color(1f, 1f, 1f, 0.25f);
            bgImage.raycastTarget = false;

            var handle = Rect("Handle", bg, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(120f, 120f), Vector2.zero);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.sprite = knob;
            handleImage.color = new Color(1f, 1f, 1f, 0.6f);
            handleImage.raycastTarget = false;

            Assign(joystick, "background", bg);
            Assign(joystick, "handle", handle);

            // 3) Crosshair — the centre-screen ray the interactor will use.
            var cross = Rect("Crosshair", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(14f, 14f), Vector2.zero);
            var crossImage = cross.gameObject.AddComponent<Image>();
            crossImage.sprite = knob;
            crossImage.color = new Color(1f, 1f, 1f, 0.85f);
            crossImage.raycastTarget = false;

            // 4) Context button (Pick up / Place / ...), bottom-right. Hidden until there's something to do.
            var ctx = Rect("ContextButton", root.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(240f, 240f), new Vector2(-240f, 220f));
            var ctxImage = ctx.gameObject.AddComponent<Image>();
            ctxImage.sprite = knob;
            ctxImage.color = new Color(0.15f, 0.85f, 0.55f, 0.95f);
            var ctxButton = ctx.gameObject.AddComponent<Button>();
            ctxButton.targetGraphic = ctxImage;
            var ctxGroup = ctx.gameObject.AddComponent<CanvasGroup>();
            ctxGroup.alpha = 0f;
            ctxGroup.blocksRaycasts = false;

            var labelRt = Rect("Label", ctx, Vector2.zero, Vector2.one, new Vector2(-30f, -30f), Vector2.zero);
            var label = labelRt.gameObject.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = 20f;
            label.fontSizeMax = 40f;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color(0.04f, 0.08f, 0.1f);
            label.raycastTarget = false;
            label.text = string.Empty;

            var ctxView = ctx.gameObject.AddComponent<ContextButtonView>();
            Assign(ctxView, "button", ctxButton);
            Assign(ctxView, "label", label);
            Assign(ctxView, "group", ctxGroup);

            return SavePrefab(root, ControlsPrefabPath);
        }

        /// <summary>Creates an EventSystem using the Input System UI module (default actions).</summary>
        public static GameObject CreateEventSystem()
        {
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            return go;
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static T LoadConfig<T>(string assetName) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>($"{ConfigFolder}/{assetName}.asset");
            if (asset == null) Debug.LogError($"[PlayerRigBuilder] Missing config {ConfigFolder}/{assetName}.asset");
            return asset;
        }

        private static void Assign(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[PlayerRigBuilder] {target.GetType().Name} has no serialized field '{field}'.", target);
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            return rt;
        }

        private static void Invisible(RectTransform rt)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = true;
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
