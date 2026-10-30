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
