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
    /// Builds <c>Hud.prefab</c>: the credits counter (kit <see cref="CoinsWidget"/>), the coin layer
    /// with the kit <see cref="UICollector"/> trail, and the <see cref="PayoutPopupPresenter"/> "+60 ¢"
    /// pop. A separate overlay canvas above the touch controls with no raycaster, so it never steals
    /// touches. Slice 6 folds this into the FSM's ShiftHud view; the pieces carry over unchanged.
    /// </summary>
    public static class HudBuilder
    {
        public const string HudPrefabPath = "Assets/OrbitalCargo/Prefabs/UI/Hud.prefab";

        [MenuItem("Tools/OrbitalCargo/Build HUD Prefab")]
        public static GameObject BuildHudPrefab()
        {
            var root = new GameObject("Hud", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

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

            var presenter = root.AddComponent<PayoutPopupPresenter>();
            Assign(presenter, "onShuttleLaunched", CoreAssetFactory.LoadEvent<GameEventWithLaunchResult>("e_ShuttleLaunched"));
            Assign(presenter, "creditsChanged", creditsChanged);
            Assign(presenter, "config", CoreAssetFactory.Load<ShiftConfigSO>(CoreAssetFactory.ShiftConfigPath));
            Assign(presenter, "canvasRect", root.transform);
            Assign(presenter, "coinSource", source);
            Assign(presenter, "coinTarget", icon);
            Assign(presenter, "payoutLabel", labelText);

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
