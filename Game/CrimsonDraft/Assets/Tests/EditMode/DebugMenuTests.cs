#nullable enable

using System.Linq;
using CrimsonDraft.Infrastructure.DebugMenu;
using NUnit.Framework;

namespace CrimsonDraft.Tests.EditMode
{
    public sealed class DebugMenuTests
    {
        private DebugMenuRegistry registry = null!;

        [SetUp]
        public void SetUp() => this.registry = new DebugMenuRegistry();

        private static string[] Labels(IDebugMenu menu) => menu.Entries.Select(e => e.Label).ToArray();

        [Test]
        public void Entries_GroupedByFirstAppearance_KeepingRegistrationOrderInsideAGroup()
        {
            this.registry.Register(DebugMenuEntry.Action("Nav", "a", () => { }));
            this.registry.Register(DebugMenuEntry.Action("Overlays", "b", () => { }));
            this.registry.Register(DebugMenuEntry.Action("Nav", "c", () => { }));

            CollectionAssert.AreEqual(new[] { "a", "c", "b" }, Labels(this.registry));
        }

        [Test]
        public void DisposingRegistration_RemovesEntryAndRaisesChanged()
        {
            int changes = 0;
            this.registry.Changed += () => changes++;

            var handle = this.registry.Register(DebugMenuEntry.Action("g", "x", () => { }));
            handle.Dispose();
            handle.Dispose(); // second dispose is a no-op

            Assert.IsEmpty(this.registry.Entries);
            Assert.AreEqual(2, changes);
        }

        [Test]
        public void Action_RunsWhenActivated()
        {
            int runs = 0;
            var entry = DebugMenuEntry.Action("g", "x", () => runs++);

            entry.Activate();

            Assert.AreEqual(1, runs);
            Assert.IsFalse(entry.IsToggle);
        }

        [Test]
        public void Toggle_FlipsStateOnEachActivate()
        {
            bool state = false;
            var entry = DebugMenuEntry.Toggle("g", "x", () => state, v => state = v);

            entry.Activate();
            Assert.IsTrue(entry.IsOn);
            entry.Activate();
            Assert.IsFalse(entry.IsOn);
            Assert.IsTrue(entry.IsToggle);
        }

        [Test]
        public void Selection_WrapsAroundBothEnds()
        {
            this.registry.Register(DebugMenuEntry.Action("g", "a", () => { }));
            this.registry.Register(DebugMenuEntry.Action("g", "b", () => { }));
            this.registry.Register(DebugMenuEntry.Action("g", "c", () => { }));
            var selection = new DebugMenuSelection(this.registry);

            selection.Move(-1);
            Assert.AreEqual("c", selection.Current!.Label);
            selection.Move(1);
            Assert.AreEqual("a", selection.Current!.Label);
        }

        [Test]
        public void Selection_StaysValidWhenEntriesDisappear()
        {
            this.registry.Register(DebugMenuEntry.Action("g", "a", () => { }));
            var last = this.registry.Register(DebugMenuEntry.Action("g", "b", () => { }));
            var selection = new DebugMenuSelection(this.registry);
            selection.Move(1);

            last.Dispose();

            Assert.AreEqual("a", selection.Current!.Label);
        }

        [Test]
        public void Selection_EmptyMenu_HasNoCurrentAndIgnoresInput()
        {
            var selection = new DebugMenuSelection(this.registry);

            selection.Move(1);
            selection.ActivateCurrent();

            Assert.IsNull(selection.Current);
        }

        [Test]
        public void Selection_ActivateCurrent_RunsTheHighlightedEntry()
        {
            string ran = "";
            this.registry.Register(DebugMenuEntry.Action("g", "a", () => ran = "a"));
            this.registry.Register(DebugMenuEntry.Action("g", "b", () => ran = "b"));
            var selection = new DebugMenuSelection(this.registry);

            selection.Move(1);
            selection.ActivateCurrent();

            Assert.AreEqual("b", ran);
        }
    }
}
