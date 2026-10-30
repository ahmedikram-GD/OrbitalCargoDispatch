using System.Collections.Generic;
using NUnit.Framework;
using OrbitalCargo.Radio;
using UnityEngine;

namespace OrbitalCargo.Tests
{
    public class RadioScriptTests
    {
        private RadioScriptSO _script;

        [SetUp]
        public void SetUp()
        {
            _script = ScriptableObject.CreateInstance<RadioScriptSO>();
            _script.Lines = new List<RadioLine>
            {
                new RadioLine { Beat = "Intro", Text = "Evening, rookie." },
                null,
                new RadioLine { Beat = "Junk", Text = "Chute it." },
            };
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_script);

        [Test]
        public void TryGet_KnownBeat_ReturnsItsLine()
        {
            Assert.IsTrue(_script.TryGet("Junk", out var line));
            Assert.AreEqual("Chute it.", line.Text);
        }

        [Test]
        public void TryGet_UnknownBeat_ReturnsFalse()
        {
            Assert.IsFalse(_script.TryGet("Volatile", out var line));
            Assert.IsNull(line);
        }

        [Test]
        public void TryGet_NullOrEmptyBeat_ReturnsFalse()
        {
            Assert.IsFalse(_script.TryGet(null, out _));
            Assert.IsFalse(_script.TryGet(string.Empty, out _));
        }
    }
}
