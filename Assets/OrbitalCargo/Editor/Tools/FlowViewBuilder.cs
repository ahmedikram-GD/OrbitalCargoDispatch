using System.IO;
using GameKit.AppFlow;
using GameKit.Architecture.Variables;
using OrbitalCargo.Flow.EditorTools;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalCargo.UI.EditorTools
{
    /// <summary>
    /// Builds the FSM view prefabs in <c>Prefabs/UI/Resources</c> — <c>PausePanel</c> and
    /// <c>ShiftSummary</c> (root Canvas with override sorting + CanvasGroup + the UIBase view, nested
    /// under the scene's 1920×1080 UIStates canvas) — and the fake-ad overlay the scene's ad service
    /// shows. Greybox styling to match the HUD; rebuilt every run.
    /// </summary>
    public static class FlowViewBuilder
    {
        public const string PausePrefabPath = "Assets/OrbitalCargo/Prefabs/UI/Resources/PausePanel.prefab";
        public const string SummaryPrefabPath = "Assets/OrbitalCargo/Prefabs/UI/Resources/ShiftSummary.prefab";

        private static readonly Color Dim = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color Card = new Color(0.06f, 0.08f, 0.12f, 0.96f);
        private static readonly Color Primary = new Color(0.15f, 0.85f, 0.55f);
        private static readonly Color Accent = new Color(1f, 0.78f, 0.2f);
        private static readonly Color Secondary = new Color(0.3f, 0.55f, 1f);
        private static readonly Color DarkText = new Color(0.04f, 0.08f, 0.1f);

        [MenuItem("Tools/OrbitalCargo/Build Flow View Prefabs")]
        public static void BuildAll()
        {
            BuildPausePanel();
            BuildSummary();
        }

        // ── Pause ──────────────────────────────────────────────────────────────

        public static GameObject BuildPausePanel()
        {
            var root = ViewRoot("PausePanel");
            Dimmer(root.transform);

            var card = Panel("Card", root.transform, new Vector2(700f, 460f));
            var title = Label("Title", card, new Vector2(0f, 130f), new Vector2(640f, 100f), 72f, Color.white);
            title.text = "PAUSED";
            var hint = Label("Hint", card, new Vector2(0f, 50f), new Vector2(640f, 50f), 30f, new Color(0.7f, 0.75f, 0.8f));
            hint.text = "The shift clock is stopped.";
            var resume = Button("ResumeButton", card, new Vector2(0f, -110f), new Vector2(460f, 130f), Primary, "RESUME", 52f);

            var view = root.AddComponent<PauseView>();
            WireViewBase(view, root);
            Assign(view, "resumeButton", resume.button);
            return Save(root, PausePrefabPath);
        }

        // ── Summary ────────────────────────────────────────────────────────────

        public static GameObject BuildSummary()
        {
            var root = ViewRoot("ShiftSummary");
            Dimmer(root.transform);

            var card = Panel("Card", root.transform, new Vector2(1100f, 980f));

            // Rows: direct children cascade in via the kit VerticalGroupAnimator.
            var rows = Rect("Rows", card, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            rows.offsetMin = new Vector2(60f, 40f);
            rows.offsetMax = new Vector2(-60f, -40f);
            var layout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var title = RowLabel(rows, "Title", 100f, 64f, Accent);
            title.text = "SHIFT 1 COMPLETE";
            var earned = StatRow(rows, "Earned", "Credits earned");
            var launches = StatRow(rows, "Launches", "Shuttles launched");
            var shipped = StatRow(rows, "Shipped", "Crates shipped");
            var items = StatRow(rows, "Items", "Contraband found");
            var fines = StatRow(rows, "Fines", "Fines");
            var balance = RowLabel(rows, "Balance", 70f, 36f, new Color(0.7f, 0.75f, 0.8f));
            balance.text = "Balance  0 ¢";

            var doubleBtn = RowButton(rows, "DoubleButton", Accent, "2× payout (watch ad)");
            var buyBtn = RowButton(rows, "BuyButton", Secondary, "Buy Grav-Sled (150 ¢)");
            var nextBtn = RowButton(rows, "NextButton", Primary, "START NEXT SHIFT");

            var animator = rows.gameObject.AddComponent<VerticalGroupAnimator>();
            animator.delayBetweenItems = 0.06f;

            var view = root.AddComponent<ShiftSummaryView>();
            WireViewBase(view, root);
            Assign(view, "titleText", title);
            Assign(view, "earnedText", earned);
            Assign(view, "launchesText", launches);
            Assign(view, "shippedText", shipped);
            Assign(view, "itemsText", items);
            Assign(view, "finesText", fines);
            Assign(view, "balanceText", balance);
            Assign(view, "doubleButton", doubleBtn.button);
            Assign(view, "doubleLabel", doubleBtn.label);
            Assign(view, "buyButton", buyBtn.button);
            Assign(view, "buyLabel", buyBtn.label);
            Assign(view, "nextButton", nextBtn.button);
            Assign(view, "rows", animator);
            return Save(root, SummaryPrefabPath);
        }

        // ── Fake ad overlay (scene) ────────────────────────────────────────────

        /// <summary>Full-screen "AD PLAYING" canvas above every FSM view; FakeRewardedAdService toggles it.</summary>
        public static GameObject BuildAdOverlay(Transform parent)
        {
            var root = new GameObject("FakeAdOverlay", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            var bg = Rect("Background", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var image = bg.gameObject.AddComponent<Image>();
            image.color = new Color(0.02f, 0.02f, 0.04f, 0.97f); // blocks every tap while the "ad" runs

            var title = Label("Title", root.transform, new Vector2(0f, 60f), new Vector2(1400f, 140f), 96f, Accent);
            title.text = "AD PLAYING…";
            var sub = Label("Subtitle", root.transform, new Vector2(0f, -60f), new Vector2(1400f, 80f), 40f, Color.white);
            sub.text = "(mock rewarded video — no SDK in the prototype)";

            root.SetActive(false);
            return root;
        }

        // ── Building blocks ────────────────────────────────────────────────────

        private static GameObject ViewRoot(string name)
        {
            var root = new GameObject(name, typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            // The overrideSorting setter is ignored on a root canvas, so it would not be saved in the prefab;
            // the serialized flag sticks and takes effect once the view is nested under UIStates.
            var canvasSo = new SerializedObject(canvas);
            canvasSo.FindProperty("m_OverrideSorting").boolValue = true;
            canvasSo.ApplyModifiedPropertiesWithoutUndo();
            canvas.sortingOrder = 20;
            root.AddComponent<CanvasGroup>();
            root.AddComponent<GraphicRaycaster>();
            return root;
        }

        private static void WireViewBase(Object view, GameObject root)
        {
            Assign(view, "currentStateSortingOrder", AssetDatabase.LoadAssetAtPath<Int>(FlowAssetFactory.SortingOrderPath));
            Assign(view, "_canvas", root.GetComponent<Canvas>());
            Assign(view, "_canvasGroup", root.GetComponent<CanvasGroup>());
        }

        // Full-screen dim that also swallows taps, so nothing behind the panel reacts.
        private static void Dimmer(Transform root)
        {
            var rt = Rect("Dimmer", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = Dim;
        }

        private static RectTransform Panel(string name, Transform parent, Vector2 size)
        {
            var rt = Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), size, Vector2.zero);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.color = Card;
            return rt;
        }

        private static TextMeshProUGUI Label(string name, Transform parent, Vector2 pos, Vector2 size, float fontSize, Color color)
        {
            var rt = Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), size, pos);
            var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        private static TextMeshProUGUI RowLabel(Transform rows, string name, float height, float fontSize, Color color)
        {
            var rt = Rect(name, rows, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            rt.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        // "Label ........ value" row; returns the value text.
        private static TextMeshProUGUI StatRow(Transform rows, string name, string caption)
        {
            var row = Rect(name, rows, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 58f;

            var left = Rect("Caption", row, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var captionText = left.gameObject.AddComponent<TextMeshProUGUI>();
            captionText.text = caption;
            captionText.fontSize = 38f;
            captionText.color = new Color(0.8f, 0.85f, 0.9f);
            captionText.alignment = TextAlignmentOptions.MidlineLeft;
            captionText.raycastTarget = false;

            var right = Rect("Value", row, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var value = right.gameObject.AddComponent<TextMeshProUGUI>();
            value.text = "0";
            value.fontSize = 42f;
            value.fontStyle = FontStyles.Bold;
            value.color = Color.white;
            value.alignment = TextAlignmentOptions.MidlineRight;
            value.raycastTarget = false;
            return value;
        }

        private static (Button button, TextMeshProUGUI label) RowButton(Transform rows, string name, Color color, string caption)
        {
            var rt = Rect(name, rows, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            rt.gameObject.AddComponent<LayoutElement>().preferredHeight = 104f;
            return MakeButton(rt, color, caption, 40f);
        }

        private static (Button button, TextMeshProUGUI label) Button(string name, Transform parent, Vector2 pos, Vector2 size, Color color, string caption, float fontSize)
        {
            var rt = Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), size, pos);
            return MakeButton(rt, color, caption, fontSize);
        }

        private static (Button button, TextMeshProUGUI label) MakeButton(RectTransform rt, Color color, string caption, float fontSize)
        {
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.color = color;
            var button = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
            button.colors = colors;

            var labelRt = Rect("Label", rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            labelRt.offsetMin = new Vector2(20f, 0f);
            labelRt.offsetMax = new Vector2(-20f, 0f);
            var label = labelRt.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = caption;
            label.fontSize = fontSize;
            label.fontStyle = FontStyles.Bold;
            label.color = DarkText;
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = 24f;
            label.fontSizeMax = fontSize;
            label.raycastTarget = false;
            return (button, label);
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return rt;
        }

        private static void Assign(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[FlowViewBuilder] {target.GetType().Name} has no serialized field '{field}'.", target);
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject Save(GameObject root, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
