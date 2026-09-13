#nullable enable

using NUnit.Framework;
using UnityEngine;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Input;

namespace CrimsonDraft.Tests
{
    public sealed class ControlSchemeServiceTests
    {
        private const string SchemeKey = "Control.Scheme";

        [SetUp]
        public void SetUp() => PlayerPrefs.DeleteKey(SchemeKey);

        [TearDown]
        public void TearDown() => PlayerPrefs.DeleteKey(SchemeKey);

        private static ControlSchemeService BuildAndInit()
        {
            var service = new ControlSchemeService();
            ((IInitializable)service).Initialize();
            return service;
        }

        [Test]
        public void Initialize_noSavedValue_defaultsToModern()
        {
            var service = BuildAndInit();

            Assert.AreEqual(ControlScheme.Modern, service.CurrentScheme);
        }

        [Test]
        public void SetScheme_updatesCurrentScheme()
        {
            var service = BuildAndInit();

            service.SetScheme(ControlScheme.Classic);

            Assert.AreEqual(ControlScheme.Classic, service.CurrentScheme);
        }

        [Test]
        public void SetScheme_persistsAcrossNewInstances()
        {
            var first = BuildAndInit();
            first.SetScheme(ControlScheme.Classic);

            var second = BuildAndInit();

            Assert.AreEqual(ControlScheme.Classic, second.CurrentScheme);
        }
    }
}
