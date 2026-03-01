using System;
using NUnit.Framework;
using VContainer.Unity;

namespace CrimsonDraft.Tests
{
    public sealed class CombatMenuControllerTests
    {
        private FakeCombatActionMenuView menuView = null!;

        [SetUp]
        public void SetUp()
        {
            this.menuView = new FakeCombatActionMenuView();
        }

        [Test]
        public void Initialize_Controller_subscribesToMenuView()
        {
            var controller = new CrimsonDraft.Combat.CombatMenuController(this.menuView);
            ((IInitializable)controller).Initialize();

            Assert.IsTrue(this.menuView.HasSubscribers);
        }

        [Test]
        public void AfterDispose_Controller_unsubscribesFromMenuView()
        {
            var controller = new CrimsonDraft.Combat.CombatMenuController(this.menuView);
            ((IInitializable)controller).Initialize();
            ((IDisposable)controller).Dispose();

            Assert.IsFalse(this.menuView.HasSubscribers);
        }

        [Test]
        public void AfterDispose_OperatorSelectedEvent_doesNotThrow()
        {
            var controller = new CrimsonDraft.Combat.CombatMenuController(this.menuView);
            ((IInitializable)controller).Initialize();
            ((IDisposable)controller).Dispose();

            Assert.DoesNotThrow(() => this.menuView.RaiseOnOperatorSelected(0));
        }

        private sealed class FakeCombatActionMenuView : CrimsonDraft.Combat.ICombatActionMenuView
        {
            public event Action<int>? OnOperatorSelected;
            public bool HasSubscribers => this.OnOperatorSelected != null;
            public void RaiseOnOperatorSelected(int index) => this.OnOperatorSelected?.Invoke(index);
        }
    }
}
