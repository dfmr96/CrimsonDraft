#nullable enable

using System;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Save;
using CrimsonDraft.Infrastructure.Scenes;
using CrimsonDraft.Navigation.Radio;

namespace CrimsonDraft.Tests
{
    public sealed class RadioConversationTests
    {
        private sealed class FakeSceneTransition : ISceneTransitionService
        {
            public bool IsInCombat { get; set; }

            public UniTask StartCombatAsync(string encounterId, ScriptableObject? encounterAsset = null,
                bool operatorsStartFull = false, Action? onScreenCovered = null) => UniTask.CompletedTask;
        }

        [Test]
        public void MarkHeard_recordsInRegistry_clearsPending_andRaisesEvent()
        {
            var registry = new RadioCallRegistry();
            var service  = new RadioConversationService(new FakeSceneTransition(), registry);
            string? raised = null;
            service.ConversationHeard += n => raised = n;

            service.Offer("radio_a");
            Assert.AreEqual("radio_a", service.PendingNode);

            service.MarkHeard("radio_a");

            Assert.IsNull(service.PendingNode);
            Assert.IsTrue(registry.IsHeard("radio_a"));
            Assert.IsTrue(service.HasHeard("radio_a"));
            Assert.AreEqual("radio_a", raised);
        }

        [Test]
        public void Offer_ignoresACallAlreadyHeard_evenAfterALoad()
        {
            var registry = new RadioCallRegistry();
            registry.LoadState(new[] { "radio_a" });   // what SaveGameLoader does with the save
            var service = new RadioConversationService(new FakeSceneTransition(), registry);

            service.Offer("radio_a");

            Assert.IsNull(service.PendingNode);
            Assert.IsFalse(service.IsRinging);
        }

        [Test]
        public void IsRinging_isFalseDuringCombat_butThePendingCallIsKept()
        {
            var combat  = new FakeSceneTransition();
            var service = new RadioConversationService(combat, new RadioCallRegistry());
            service.Offer("radio_a");

            Assert.IsTrue(service.IsRinging);
            combat.IsInCombat = true;
            Assert.IsFalse(service.IsRinging);
            Assert.AreEqual("radio_a", service.PendingNode);
            combat.IsInCombat = false;
            Assert.IsTrue(service.IsRinging);
        }

        [Test]
        public void Registry_roundTripsThroughSaveData_andResets()
        {
            var registry = new RadioCallRegistry();
            registry.SetHeard("radio_a");
            registry.SetHeard("radio_b");

            var data = new SaveGameData();
            data.heardRadioCallIds.AddRange(registry.HeardIds);

            var restored = new RadioCallRegistry();
            restored.LoadState(data.heardRadioCallIds);
            Assert.IsTrue(restored.IsHeard("radio_a"));
            Assert.IsTrue(restored.IsHeard("radio_b"));

            restored.ClearAll();
            Assert.IsFalse(restored.IsHeard("radio_a"));
        }
    }
}
