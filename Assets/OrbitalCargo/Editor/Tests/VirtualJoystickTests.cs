using NUnit.Framework;
using OrbitalCargo.FirstPerson;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace OrbitalCargo.Tests
{
    /// <summary>
    /// Floating-stick regression: pressing anywhere in the touch zone must centre the stick under the
    /// finger (zero input), whatever the background's anchors are. No Canvas is used — with a null UI
    /// camera RectTransformUtility treats screen points as world points, so the maths is exercised
    /// without needing a rendered canvas in edit mode.
    /// </summary>
    public class VirtualJoystickTests
    {
        private GameObject _root;
        private VirtualJoystick _joystick;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Root", typeof(RectTransform));

            // Zone: 800×600, pivot centred at world (0,0) — same shape as the Controls prefab's zone.
            var zone = new GameObject("Joystick", typeof(RectTransform));
            var zoneRt = (RectTransform)zone.transform;
            zoneRt.SetParent(_root.transform, false);
            zoneRt.sizeDelta = new Vector2(800f, 600f);

            // Background anchored to the zone's bottom-left corner, exactly as PlayerRigBuilder does.
            var bg = new GameObject("Background", typeof(RectTransform));
            var bgRt = (RectTransform)bg.transform;
            bgRt.SetParent(zoneRt, false);
            bgRt.anchorMin = bgRt.anchorMax = Vector2.zero;
            bgRt.sizeDelta = new Vector2(280f, 280f);
            bgRt.anchoredPosition = new Vector2(260f, 240f);

            _joystick = zone.AddComponent<VirtualJoystick>();
            var so = new SerializedObject(_joystick);
            so.FindProperty("background").objectReferenceValue = bgRt;
            so.FindProperty("floating").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void PointerDown_Floating_StickCentresUnderFinger_ValueIsZero()
        {
            _joystick.OnPointerDown(Pointer(new Vector2(100f, 50f)));

            Assert.AreEqual(0f, _joystick.Value.magnitude, 1e-3f);
        }

        [Test]
        public void Drag_HalfRadiusRightOfPress_ValueXIsHalf()
        {
            _joystick.OnPointerDown(Pointer(new Vector2(100f, 50f)));
            _joystick.OnDrag(Pointer(new Vector2(170f, 50f))); // radius = 140 → 70 px = 0.5

            Assert.AreEqual(0.5f, _joystick.Value.x, 1e-3f);
            Assert.AreEqual(0f, _joystick.Value.y, 1e-3f);
        }

        private static PointerEventData Pointer(Vector2 position) =>
            new PointerEventData(null) { position = position, pointerId = 0 };
    }
}
