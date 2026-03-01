using System;
using NUnit.Framework;
using UnityEngine;
using VContainer.Unity;
using CrimsonDraft.Combat;
using CrimsonDraft.Infrastructure.Events;

namespace CrimsonDraft.Tests
{
    public sealed class CombatMenuControllerTests
    {
        private FakeCombatActionMenuView menuView      = null!;
        private FakeCommandPanelView     commandPanel  = null!;
        private FakeSubPanelView         subPanel      = null!;
        private FakePublisher            publisher     = null!;

        [SetUp]
        public void SetUp()
        {
            this.menuView     = new FakeCombatActionMenuView();
            this.commandPanel = new FakeCommandPanelView();
            this.subPanel     = new FakeSubPanelView();
            this.publisher    = new FakePublisher();
        }

        private CombatMenuController BuildAndInit()
        {
            var controller = new CombatMenuController(
                this.menuView, this.commandPanel, this.subPanel, this.publisher);
            ((IInitializable)controller).Initialize();
            return controller;
        }

        // ── Existing subscription tests ────────────────────────────────

        [Test]
        public void Initialize_subscribesToMenuView()
        {
            BuildAndInit();
            Assert.IsTrue(this.menuView.HasSubscribers);
        }

        [Test]
        public void AfterDispose_unsubscribesFromMenuView()
        {
            var c = BuildAndInit();
            ((IDisposable)c).Dispose();
            Assert.IsFalse(this.menuView.HasSubscribers);
        }

        [Test]
        public void AfterDispose_operatorSelectedEvent_doesNotThrow()
        {
            var c = BuildAndInit();
            ((IDisposable)c).Dispose();
            Assert.DoesNotThrow(() => this.menuView.RaiseOnOperatorSelected(0));
        }

        // ── State machine ──────────────────────────────────────────────

        [Test]
        public void OperatorSelected_showsCommandPanel()
        {
            BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(1);
            Assert.IsTrue(this.commandPanel.IsVisible);
        }

        [Test]
        public void CommandSelected_Reload_showsSubPanel()
        {
            BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Reload);
            Assert.IsTrue(this.subPanel.IsVisible);
        }

        [Test]
        public void CommandSelected_Items_showsSubPanel()
        {
            BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Items);
            Assert.IsTrue(this.subPanel.IsVisible);
        }

        [Test]
        public void CommandSelected_Defend_showsSubPanel()
        {
            BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Defend);
            Assert.IsTrue(this.subPanel.IsVisible);
        }

        [Test]
        public void Cancel_inSubPanel_hidesSubPanel_commandPanelRemains()
        {
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            this.commandPanel.RaiseOnCommandSelected(CombatCommand.Reload);
            c.HandleCancelPressed();
            Assert.IsFalse(this.subPanel.IsVisible);
            Assert.IsTrue(this.commandPanel.IsVisible);
        }

        [Test]
        public void Cancel_inCommandPanel_hidesCommandPanel()
        {
            var c = BuildAndInit();
            this.menuView.RaiseOnOperatorSelected(0);
            c.HandleCancelPressed();
            Assert.IsFalse(this.commandPanel.IsVisible);
        }

        [Test]
        public void Cancel_inOperatorSelection_publishesCombatEndedEvent()
        {
            var c = BuildAndInit();
            c.HandleCancelPressed();
            Assert.IsTrue(this.publisher.Published);
        }

        // ── Fakes ──────────────────────────────────────────────────────

        private sealed class FakeCombatActionMenuView : ICombatActionMenuView
        {
            public event Action<int>? OnOperatorSelected;
            public bool HasSubscribers => this.OnOperatorSelected != null;
            public void RaiseOnOperatorSelected(int index) => this.OnOperatorSelected?.Invoke(index);
            public void FocusOperator(int index) { }
            public void MoveSelectorTo(RectTransform anchor) { }
            public void SetDimmed(bool dimmed) { }
            public RectTransform GetOperatorAnchor(int index) =>
                new GameObject().AddComponent<RectTransform>();
            public RectTransform GetOperatorRect(int index) =>
                new GameObject().AddComponent<RectTransform>();
        }

        private sealed class FakeCommandPanelView : ICommandPanelView
        {
            public event Action<CombatCommand>?  OnCommandSelected;
            public event Action<RectTransform>?  OnEntryFocused;
            public bool IsVisible { get; private set; }
            private readonly RectTransform panelRect = new GameObject().AddComponent<RectTransform>();
            public RectTransform PanelRect => this.panelRect;
            public void Show(RectTransform _) => this.IsVisible = true;
            public void Focus()               { }
            public void SetDimmed(bool _)     { }
            public void Hide()                => this.IsVisible = false;
            public void RaiseOnCommandSelected(CombatCommand cmd) => this.OnCommandSelected?.Invoke(cmd);
        }

        private sealed class FakeSubPanelView : ISubPanelView
        {
            public event Action<int>?           OnItemSelected;
            public event Action<RectTransform>? OnEntryFocused;
            public bool IsVisible { get; private set; }
            public void Show(SubPanelItem[] _, RectTransform __) => this.IsVisible = true;
            public void Hide()                                    => this.IsVisible = false;
        }

        private sealed class FakePublisher : MessagePipe.IPublisher<CombatEndedEvent>
        {
            public bool Published { get; private set; }
            public void Publish(CombatEndedEvent message) => this.Published = true;
        }
    }
}
