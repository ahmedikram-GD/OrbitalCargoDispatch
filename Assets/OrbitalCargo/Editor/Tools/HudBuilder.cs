using System.IO;
using GameKit.Architecture.Events;
using GameKit.Architecture.PoolSystem;
using GameKit.Architecture.Variables;
using GameKit.Haptics;
using GameKit.UI.Animations;
using GameKit.UI.Header;
using OrbitalCargo.Config;
using OrbitalCargo.Economy;
using OrbitalCargo.Shift.EditorTools;
using THEBADDEST.SoundSystem;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalCargo.UI.EditorTools
{
    /// <summary>
    /// Builds <c>Resources/ShiftHud.prefab</c>, the FSM-pooled HUD (<see cref="HudView"/> : kit GameHud):
    /// credits counter (kit <see cref="CoinsWidget"/>), coin layer with the kit <see cref="UICollector"/>
    /// trail, the <see cref="PayoutPopupPresenter"/> "+60 ¢" pop, collection card, news ticker, the shift
    /// label + mm:ss timer and the pause button. Root = Canvas (override sorting; the FSM sets the order)
    /// + CanvasGroup + raycaster; everything but the pause button is raycast-transparent so the touch
    /// controls underneath keep working.
    /// </summary>
    public static class HudBuilder
    {
        public const string HudPrefabPath = "Assets/OrbitalCargo/Prefabs/UI/Resources/ShiftHud.prefab";
        private const string LegacyHudPrefabPath = "Assets/OrbitalCargo/Prefabs/UI/Hud.prefab";

        [MenuItem("Tools/OrbitalCargo/Build HUD Prefab")]
        public static GameObject BuildHudPrefab()
        {
            // Nested under the scene's UIStates canvas, which owns the 1920×1080 scaler.
            var root = new GameObject("ShiftHud", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            // The overrideSorting setter is ignored on a root canvas, so it would not be saved in the prefab;
            // the serialized flag sticks and takes effect once the view is nested under UIStates.
            var canvasSo = new SerializedObject(canvas);
            canvasSo.FindProperty("m_OverrideSorting").boolValue = true;
            canvasSo.ApplyModifiedPropertiesWithoutUndo();
            canvas.sortingOrder = 10;
            root.AddComponent<CanvasGroup>();
            root.AddComponent<GraphicRaycaster>();

            var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            var panelSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            var haptics = CoreAssetFactory.Load<HapticHandlerSO>(CoreAssetFactory.HapticsPath);
            var sounds = CoreAssetFactory.Load<SoundService>(CoreAssetFactory.SoundsPath);
            var creditsChanged = CoreAssetFactory.LoadEvent<GameEvent>("e_CreditsChanged");

            // ── Credits panel (top-left) ──
            var panel = Rect("CreditsPanel", root.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(320f, 96f), new Vector2(40f, -30f), new Vector2(0f, 1f));
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.sprite = panelSprite;
            panelImage.type = Image.Type.Sliced;
            panelImage.color = new Color(0.05f, 0.07f, 0.1f, 0.75f);
            panelImage.raycastTarget = false;

            var icon = Rect("Icon", panel, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(64f, 64f), new Vector2(52f, 0f), new Vector2(0.5f, 0.5f));
            var iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.sprite = knob;
            iconImage.color = new Color(1f, 0.82f, 0.2f);
            iconImage.raycastTarget = false;

            var amount = Rect("Amount", panel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            amount.offsetMin = new Vector2(100f, 0f);
            amount.offsetMax = new Vector2(-20f, 0f);
            var amountText = amount.gameObject.AddComponent<TextMeshProUGUI>();
            amountText.text = "0";
            amountText.fontSize = 52f;
            amountText.fontStyle = FontStyles.Bold;
            amountText.alignment = TextAlignmentOptions.MidlineLeft;
            amountText.color = Color.white;
            amountText.raycastTarget = false;

            var widget = panel.gameObject.AddComponent<CoinsWidget>();
            Assign(widget, "softCurrency", CoreAssetFactory.Load<DBInt>(CoreAssetFactory.CreditsPath));
            Assign(widget, "balanceChangedEvent", creditsChanged);
            Assign(widget, "balanceText", amountText);
            Assign(widget, "pulseRoot", panel);
            Assign(widget, "iconPulse", icon);
            Assign(widget, "haptics", haptics);

            // ── Coin layer: coin trail + payout pop ──
            var layer = Rect("CoinLayer", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            var source = Rect("CoinSource", layer, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(10f, 10f), Vector2.zero, new Vector2(0.5f, 0.5f));

            var label = Rect("PayoutLabel", layer, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(420f, 110f), Vector2.zero, new Vector2(0.5f, 0.5f));
            var labelText = label.gameObject.AddComponent<TextMeshProUGUI>();
            labelText.text = "+0 ¢";
            labelText.fontSize = 72f;
            labelText.fontStyle = FontStyles.Bold;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.color = new Color(1f, 0.85f, 0.25f);
            labelText.outlineWidth = 0.2f;
            labelText.outlineColor = new Color32(40, 25, 0, 255);
            labelText.raycastTarget = false;

            var collector = layer.gameObject.AddComponent<UICollector>();
            Assign(collector, "coinsHolder", layer);
            Assign(collector, "poolManager", CoreAssetFactory.Load<PoolManagerSO>(CoreAssetFactory.UIPoolerPath));
            Assign(collector, "canvas", canvas);
            Assign(collector, "soundService", sounds);
            Assign(collector, "hapticHandlerSo", haptics);

            BuildCollectionPopup(root.transform, panelSprite, knob);
            BuildNewsBanner(root.transform, panelSprite);
            BuildRadioCard(root.transform, panelSprite);

            var presenter = root.AddComponent<PayoutPopupPresenter>();
            Assign(presenter, "onShuttleLaunched", CoreAssetFactory.LoadEvent<GameEventWithLaunchResult>("e_ShuttleLaunched"));
            Assign(presenter, "creditsChanged", creditsChanged);
            Assign(presenter, "config", CoreAssetFactory.Load<ShiftConfigSO>(CoreAssetFactory.ShiftConfigPath));
            Assign(presenter, "canvasRect", root.transform);
            Assign(presenter, "coinSource", source);
            Assign(presenter, "coinTarget", icon);
            Assign(presenter, "payoutLabel", labelText);

            var timer = BuildShiftPanel(root.transform, panelSprite);
            var pause = BuildPauseButton(root.transform, panelSprite);

            var view = root.AddComponent<HudView>();
            Assign(view, "currentStateSortingOrder", CoreAssetFactory.Load<Int>(Flow.EditorTools.FlowAssetFactory.SortingOrderPath));
            Assign(view, "onShiftTick", CoreAssetFactory.LoadEvent<GameEventWithInt>("e_ShiftTick"));
            Assign(view, "timerText", timer.timer);
            Assign(view, "shiftText", timer.shift);
            Assign(view, "pauseButton", pause);

            if (AssetDatabase.LoadAssetAtPath<GameObject>(LegacyHudPrefabPath) != null) AssetDatabase.DeleteAsset(LegacyHudPrefabPath);
            Directory.CreateDirectory(Path.GetDirectoryName(HudPrefabPath));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // "Added to collection" card, upper centre, hidden until e_ItemCollected.
        private static void BuildCollectionPopup(Transform root, Sprite panelSprite, Sprite knob)
        {
            var card = Rect("CollectionPopup", root, new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f), new Vector2(680f, 150f), Vector2.zero, new Vector2(0.5f, 0.5f));
            var bg = card.gameObject.AddComponent<Image>();
            bg.sprite = panelSprite;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.06f, 0.12f, 0.08f, 0.92f);
            bg.raycastTarget = false;
            var group = card.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            var icon = Rect("Icon", card, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(96f, 96f), new Vector2(80f, 0f), new Vector2(0.5f, 0.5f));
            var iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.sprite = knob;
            iconImage.raycastTarget = false;

            var title = Text("Title", card, new Vector2(150f, 10f), new Vector2(-20f, -10f), 42f, new Color(0.55f, 1f, 0.45f), TextAlignmentOptions.TopLeft);
            title.text = "Added to collection";
            var subtitle = Text("Subtitle", card, new Vector2(150f, 18f), new Vector2(-20f, -70f), 32f, Color.white, TextAlignmentOptions.BottomLeft);
            subtitle.text = "Alien Egg";

            var view = card.gameObject.AddComponent<CollectionPopupView>();
            Assign(view, "onItemCollected", Crates.EditorTools.CrateAssetFactory.EnsureHiddenItemEvent("e_ItemCollected"));
            Assign(view, "card", card);
            Assign(view, "group", group);
            Assign(view, "icon", iconImage);
            Assign(view, "title", title);
            Assign(view, "subtitle", subtitle);
        }

        // "BREAKING" news ticker, drops from the top edge on e_NewsAlert.
        private static void BuildNewsBanner(Transform root, Sprite panelSprite)
        {
            var banner = Rect("NewsBanner", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1500f, 110f), new Vector2(0f, 140f), new Vector2(0.5f, 0.5f));
            var bg = banner.gameObject.AddComponent<Image>();
            bg.sprite = panelSprite;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.65f, 0.05f, 0.05f, 0.95f);
            bg.raycastTarget = false;

            var label = Text("Label", banner, new Vector2(30f, 0f), new Vector2(-30f, 0f), 40f, Color.white, TextAlignmentOptions.Center);
            label.enableAutoSizing = true;
            label.fontSizeMin = 24f;
            label.fontSizeMax = 40f;
            label.text = "BREAKING";

            var view = banner.gameObject.AddComponent<NewsAlertView>();
            Assign(view, "onNewsAlert", CoreAssetFactory.LoadEvent<GameEventWithString>("e_NewsAlert"));
            Assign(view, "banner", banner);
            Assign(view, "label", label);
        }

        // Rook's radio card, top-left under the credits: speaker tag + typewriter line.
        private static void BuildRadioCard(Transform root, Sprite panelSprite)
        {
            var card = Rect("RadioCard", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(820f, 150f), new Vector2(40f, -140f), new Vector2(0f, 1f));
            var bg = card.gameObject.AddComponent<Image>();
            bg.sprite = panelSprite;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.04f, 0.1f, 0.08f, 0.85f);
            bg.raycastTarget = false;
            var group = card.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var speaker = Text("Speaker", card, new Vector2(24f, 104f), new Vector2(-24f, -8f), 30f, new Color(0.45f, 1f, 0.6f), TextAlignmentOptions.TopLeft);
            speaker.text = "ROOK";
            var line = Text("Line", card, new Vector2(24f, 12f), new Vector2(-24f, -46f), 32f, Color.white, TextAlignmentOptions.TopLeft);
            line.fontStyle = FontStyles.Normal;
            line.text = "Evening, rookie.";

            var view = card.gameObject.AddComponent<RadioLineView>();
            Assign(view, "script", Radio.EditorTools.RadioAssetFactory.EnsureScript());
            Assign(view, "onRadioBeat", CoreAssetFactory.LoadEvent<GameEventWithString>("e_RadioBeat"));
            Assign(view, "card", group);
            Assign(view, "speakerText", speaker);
            Assign(view, "lineText", line);
        }

        // Shift label over a mm:ss timer, top centre.
        private static (TextMeshProUGUI shift, TextMeshProUGUI timer) BuildShiftPanel(Transform root, Sprite panelSprite)
        {
            var panel = Rect("ShiftPanel", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(300f, 130f), new Vector2(0f, -30f), new Vector2(0.5f, 1f));
            var bg = panel.gameObject.AddComponent<Image>();
            bg.sprite = panelSprite;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.05f, 0.07f, 0.1f, 0.75f);
            bg.raycastTarget = false;

            var shift = Text("Shift", panel, new Vector2(10f, 78f), new Vector2(-10f, -6f), 30f, new Color(0.6f, 0.8f, 1f), TextAlignmentOptions.Center);
            shift.text = "SHIFT 1";
            var timer = Text("Timer", panel, new Vector2(10f, 6f), new Vector2(-10f, -44f), 64f, Color.white, TextAlignmentOptions.Center);
            timer.text = "5:00";
            return (shift, timer);
        }

        // Pause, top-right: the only raycast target on the HUD.
        private static Button BuildPauseButton(Transform root, Sprite panelSprite)
        {
            var rt = Rect("PauseButton", root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(110f, 110f), new Vector2(-40f, -30f), new Vector2(1f, 1f));
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = panelSprite;
            image.type = Image.Type.Sliced;
            image.color = new Color(0.05f, 0.07f, 0.1f, 0.75f);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var label = Text("Label", rt, Vector2.zero, Vector2.zero, 56f, Color.white, TextAlignmentOptions.Center);
            label.text = "II";
            return button;
        }

        private static TextMeshProUGUI Text(string name, Transform parent, Vector2 offsetMin, Vector2 offsetMax, float size, Color color, TextAlignmentOptions align)
        {
            var rt = Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.color = color;
            text.alignment = align;
            text.raycastTarget = false;
            return text;
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 pos, Vector2 pivot)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
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
                Debug.LogError($"[HudBuilder] {target.GetType().Name} has no serialized field '{field}'.", target);
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
