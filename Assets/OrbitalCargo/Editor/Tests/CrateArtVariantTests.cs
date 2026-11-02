using NUnit.Framework;
using OrbitalCargo.Config;
using OrbitalCargo.Crates;
using UnityEditor;
using UnityEngine;

namespace OrbitalCargo.Tests
{
    /// <summary>Protects scanner visibility and pooled type changes when an authored exterior is present.</summary>
    public sealed class CrateArtVariantTests
    {
        private GameObject _root;
        private GameObject _detail;
        private CrateVisuals _visuals;
        private Renderer _casing, _stripe, _legacy;
        private CrateTypeSO _standard, _other;
        private Material _material;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Crate art test");
            _visuals = _root.AddComponent<CrateVisuals>();
            _standard = ScriptableObject.CreateInstance<CrateTypeSO>();
            _other = ScriptableObject.CreateInstance<CrateTypeSO>();
            _material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            _standard.CasingMaterial = _material;
            _other.CasingMaterial = _material;
            _casing = NewRenderer("Casing");
            _stripe = NewRenderer("Stripe");
            _legacy = NewRenderer("Legacy");
            _detail = new GameObject("Detailed exterior");
            _detail.transform.SetParent(_root.transform);
            var so = new SerializedObject(_visuals);
            so.FindProperty("casing").objectReferenceValue = _casing;
            so.FindProperty("stripe").objectReferenceValue = _stripe;
            so.FindProperty("detailedType").objectReferenceValue = _standard;
            so.FindProperty("detailedModel").objectReferenceValue = _detail;
            var details = so.FindProperty("legacyDetails");
            details.arraySize = 1;
            details.GetArrayElementAtIndex(0).objectReferenceValue = _legacy;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_standard);
            Object.DestroyImmediate(_other);
            Object.DestroyImmediate(_material);
        }

        [Test]
        public void SetXRay_WithDetailedType_HidesExteriorAndShowsTransparentProxy()
        {
            _visuals.Apply(_standard);
            Assert.That(_detail.activeSelf, Is.True);
            Assert.That(_casing.enabled, Is.False);
            _visuals.SetXRay(true, _material);
            Assert.That(_detail.activeSelf, Is.False);
            Assert.That(_casing.enabled, Is.True);
            Assert.That(_stripe.enabled, Is.False);
            Assert.That(_legacy.enabled, Is.False);
            _visuals.SetXRay(false, null);
            Assert.That(_detail.activeSelf, Is.True);
            Assert.That(_casing.enabled, Is.False);
        }

        [Test]
        public void Apply_AfterScannedStandardIsReusedForOtherType_RestoresLegacyLook()
        {
            _visuals.Apply(_standard);
            _visuals.SetXRay(true, _material);
            _visuals.Apply(_other);
            Assert.That(_visuals.IsXRay, Is.False);
            Assert.That(_detail.activeSelf, Is.False);
            Assert.That(_casing.enabled, Is.True);
            Assert.That(_stripe.enabled, Is.True);
            Assert.That(_legacy.enabled, Is.True);
            _visuals.Apply(_standard);
            Assert.That(_detail.activeSelf, Is.True);
            Assert.That(_legacy.enabled, Is.False);
        }

        [Test]
        public void SetXRay_WithMissingMaterial_KeepsDetailedExteriorVisible()
        {
            _visuals.Apply(_standard);
            _visuals.SetXRay(true, null);
            Assert.That(_visuals.IsXRay, Is.False);
            Assert.That(_detail.activeSelf, Is.True);
        }

        private Renderer NewRenderer(string name)
        {
            var go = new GameObject(name, typeof(MeshRenderer));
            go.transform.SetParent(_root.transform);
            return go.GetComponent<Renderer>();
        }
    }
}
