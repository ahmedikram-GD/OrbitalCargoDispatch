using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace OrbitalCargo.UI.EditorTools
{
    /// <summary>
    /// Builds the <see cref="TutorialOverlayView"/> inside the Controls prefab (called by
    /// <c>PlayerRigBuilder.BuildControlsPrefab</c>): a nested canvas sorted above the HUD holding the
    /// centre lesson card (title, body, progress bar), the top-right objective card (crate colour swatch
    /// + text), the pointing hand and the look-gesture arrow. Living on Controls means it hides with the
    /// touch controls on pause/summary and can point at the real joystick and context button. Nothing
    /// in it is a raycast target. Hand and arrow sprites were copied from the studio kits
    /// (NumberixPuzzle GameKit onboarding hand, Silly-Justice tutorial arrow).
    /// </summary>
    public static class TutorialOverlayBuilder
    {
        private const string HandSpritePath = "Assets/OrbitalCargo/Art/Sprites/TutorialHand.png";
        private const string LookArrowSpritePath = "Assets/OrbitalCargo/Art/Sprites/TutorialLookArrow.png";
        // The hand sprite's fingertip, as a pivot (so positioning the hand positions the fingertip).
        private static readonly Vector2 HandFingertip = new Vector2(0.085f, 0.945f);
        private static readonly Color PanelColor = new Color(0.04f, 0.06f, 0.09f, 0.88f);
        private static readonly Color AccentGreen = new Color(0.6f, 1f, 0.35f);

        public static TutorialOverlayView Build(Transform controlsRoot, RectTransform joystickTarget, RectTransform tapTarget)
        {
            var panelSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var root = Rect("TutorialOverlay", controlsRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            // Own canvas so it draws over the HUD (sorting 10); the serialized flag is what sticks in a prefab.
            var canvas = root.gameObject.AddComponent<Canvas>();
            var canvasSo = new SerializedObject(canvas);
            canvasSo.FindProperty("m_OverrideSorting").boolValue = true;
            canvasSo.FindProperty("m_SortingOrder").intValue = 20;
            canvasSo.ApplyModifiedPropertiesWithoutUndo();

            // ── Centre card ──
            var card = Rect("Card", root, new Vector2(0.5f, 0.64f), new Vector2(0.5f, 0.64f), new Vector2(760f, 280f), Vector2.zero, new Vector2(0.5f, 0.5f));
            Panel(card, panelSprite, PanelColor);
            var cardGroup = Group(card);
            var cardTitle = Text("Title", card, new Vector2(30f, 190f), new Vector2(-30f, -16f), 58f, AccentGreen, TextAlignmentOptions.Center);
            cardTitle.text = "MOVE AROUND";
            var cardBody = Text("Body", card, new Vector2(40f, 50f), new Vector2(-40f, -96f), 42f, Color.white, TextAlignmentOptions.Center);
            cardBody.enableAutoSizing = true;
            cardBody.fontSizeMin = 28f;
            cardBody.fontSizeMax = 42f;
            cardBody.text = "Use your left thumb to move around.";

            var barBg = Rect("ProgressBg", card, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(600f, 16f), new Vector2(0f, 26f), new Vector2(0.5f, 0.5f));
            Panel(barBg, panelSprite, new Color(1f, 1f, 1f, 0.12f));
            var barFill = Rect("Fill", barBg, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            var fill = barFill.gameObject.AddComponent<Image>();
            fill.sprite = panelSprite;
            fill.color = AccentGreen;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            fill.raycastTarget = false;

            // ── Objective card (top-right, under the pause button) ──
            var obj = Rect("Objective", root, Vector2.one, Vector2.one, new Vector2(680f, 160f), new Vector2(-40f, -160f), Vector2.one);
            Panel(obj, panelSprite, PanelColor);
            var objGroup = Group(obj);
            var swatchFrame = Rect("SwatchFrame", obj, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(116f, 116f), new Vector2(80f, 0f), new Vector2(0.5f, 0.5f));
            Panel(swatchFrame, panelSprite, Color.white);
            var swatchRt = Rect("Swatch", swatchFrame, Vector2.zero, Vector2.one, new Vector2(-12f, -12f), Vector2.zero, new Vector2(0.5f, 0.5f));
            var swatch = Panel(swatchRt, panelSprite, Color.grey);
            var objTitle = Text("Title", obj, new Vector2(158f, 100f), new Vector2(-20f, -10f), 36f, AccentGreen, TextAlignmentOptions.TopLeft);
            objTitle.text = "GREY CRATE";
            var objBody = Text("Body", obj, new Vector2(158f, 12f), new Vector2(-20f, -56f), 32f, Color.white, TextAlignmentOptions.TopLeft);
            objBody.fontStyle = FontStyles.Normal;
            objBody.enableAutoSizing = true;
            objBody.fontSizeMin = 22f;
            objBody.fontSizeMax = 32f;
            objBody.text = "Grey crates go to BAY A.";

            // ── Look arrow + hand (hand last: draws on top) ──
            var arrowRt = Rect("LookArrow", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(380f, 102f), Vector2.zero, new Vector2(0.5f, 0.5f));
            var arrowImage = arrowRt.gameObject.AddComponent<Image>();
            arrowImage.sprite = LoadSprite(LookArrowSpritePath);
            arrowImage.raycastTarget = false;

            var handRt = Rect("Hand", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(150f, 155f), Vector2.zero, HandFingertip);
            var handImage = handRt.gameObject.AddComponent<Image>();
            handImage.sprite = LoadSprite(HandSpritePath);
            handImage.raycastTarget = false;
            var handGroup = Group(handRt);

            var view = root.gameObject.AddComponent<TutorialOverlayView>();
            Assign(view, "card", cardGroup);
            Assign(view, "cardTitle", cardTitle);
            Assign(view, "cardBody", cardBody);
            Assign(view, "cardProgress", fill);
            Assign(view, "objective", objGroup);
            Assign(view, "objectiveTitle", objTitle);
            Assign(view, "objectiveBody", objBody);
            Assign(view, "objectiveSwatch", swatch);
            Assign(view, "hand", handRt);
            Assign(view, "handGroup", handGroup);
            Assign(view, "lookArrow", arrowRt);
            Assign(view, "joystickTarget", joystickTarget);
            Assign(view, "tapTarget", tapTarget);
            return view;
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static Sprite LoadSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[TutorialOverlayBuilder] Missing sprite texture {path}.");
                return null;
            }
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 512;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Image Panel(RectTransform rt, Sprite sprite, Color color)
        {
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static CanvasGroup Group(RectTransform rt)
        {
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            return group;
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
                Debug.LogError($"[TutorialOverlayBuilder] {target.GetType().Name} has no serialized field '{field}'.", target);
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
