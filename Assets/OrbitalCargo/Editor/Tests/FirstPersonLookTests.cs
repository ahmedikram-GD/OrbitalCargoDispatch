using NUnit.Framework;
using OrbitalCargo.FirstPerson;
using UnityEditor;
using UnityEngine;

namespace OrbitalCargo.Tests
{
    /// <summary>
    /// <see cref="FirstPersonLook.AimTowards"/> must pitch from the CAMERA (eye height), not the body's
    /// feet — otherwise a crate on the belt below eye level makes the view look up.
    /// </summary>
    public class FirstPersonLookTests
    {
        private GameObject _body;
        private LookConfigSO _config;
        private FirstPersonLook _look;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<LookConfigSO>();
            _body = new GameObject("Body");
            var cam = new GameObject("Camera").transform;
            cam.SetParent(_body.transform, false);
            cam.localPosition = new Vector3(0f, 1.6f, 0f);

            _look = _body.AddComponent<FirstPersonLook>();
            var so = new SerializedObject(_look);
            so.FindProperty("config").objectReferenceValue = _config;
            so.FindProperty("cameraPivot").objectReferenceValue = cam;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_body);
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void AimTowards_PointBelowEyeLevel_PitchesDown()
        {
            // Eye at y=1.6; target 1.6 m ahead at y=1.2 → 0.4 m drop → atan(0.4/1.6) ≈ 14.04° down.
            _look.AimTowards(new Vector3(0f, 1.2f, 1.6f), 1f);

            Assert.AreEqual(14.04f, _look.Pitch, 0.1f);
        }

        [Test]
        public void AimTowards_PointAtEyeLevel_PitchIsLevel()
        {
            _look.AimTowards(new Vector3(0f, 1.6f, 3f), 1f);

            Assert.AreEqual(0f, _look.Pitch, 0.1f);
        }
    }
}
