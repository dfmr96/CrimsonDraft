using System;
using NUnit.Framework;
using VContainer.Unity;

namespace CrimsonDraft.Tests
{
    public sealed class CombatMenuControllerTests
    {
        private FakeCombatActionMenuView menuView = null!;
        private FakeQTEView qteView = null!;

        [SetUp]
        public void SetUp()
        {
            this.menuView = new FakeCombatActionMenuView();
            this.qteView = new FakeQTEView();
        }

        [Test]
        public void QTEPanel_StartsHidden()
        {
            IInitializable controller = new CrimsonDraft.Combat.CombatMenuController(this.menuView, this.qteView);
            controller.Initialize();

            Assert.IsFalse(this.qteView.IsVisible);
        }

        [Test]
        public void DisparasEvent_ShowsQTEPanel()
        {
            IInitializable controller = new CrimsonDraft.Combat.CombatMenuController(this.menuView, this.qteView);
            controller.Initialize();

            this.menuView.RaiseOnDisparar();

            Assert.IsTrue(this.qteView.IsVisible);
        }

        [Test]
        public void CerrarEvent_HidesQTEPanel()
        {
            IInitializable controller = new CrimsonDraft.Combat.CombatMenuController(this.menuView, this.qteView);
            controller.Initialize();
            this.menuView.RaiseOnDisparar();

            this.menuView.RaiseOnCerrar();

            Assert.IsFalse(this.qteView.IsVisible);
        }

        [Test]
        public void AfterDispose_EventsNoLongerTriggerView()
        {
            var controller = new CrimsonDraft.Combat.CombatMenuController(this.menuView, this.qteView);
            ((IInitializable)controller).Initialize();
            ((IDisposable)controller).Dispose();

            this.menuView.RaiseOnDisparar();

            Assert.IsFalse(this.qteView.IsVisible);
        }

        private sealed class FakeCombatActionMenuView : CrimsonDraft.Combat.ICombatActionMenuView
        {
            public event Action? OnDisparar;
            public event Action? OnCerrar;
            public void RaiseOnDisparar() => OnDisparar?.Invoke();
            public void RaiseOnCerrar() => OnCerrar?.Invoke();
        }

        private sealed class FakeQTEView : CrimsonDraft.Combat.IQTEView
        {
            public bool IsVisible { get; private set; }
            public void Show() => this.IsVisible = true;
            public void Hide() => this.IsVisible = false;
        }
    }
}
