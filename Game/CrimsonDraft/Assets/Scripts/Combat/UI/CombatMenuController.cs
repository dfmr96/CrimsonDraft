#nullable enable

using UnityEngine.Scripting;
using VContainer.Unity;

namespace CrimsonDraft.Combat
{
    public sealed class CombatMenuController : IInitializable, System.IDisposable
    {
        #region Dependency Injection

        private readonly ICombatActionMenuView menuView;

        [Preserve]
        public CombatMenuController(ICombatActionMenuView menuView)
        {
            this.menuView = menuView;
        }

        #endregion

        #region IInitializable

        void IInitializable.Initialize()
        {
            this.menuView.OnOperatorSelected += this.HandleOperatorSelected;
        }

        #endregion

        #region IDisposable

        void System.IDisposable.Dispose()
        {
            this.menuView.OnOperatorSelected -= this.HandleOperatorSelected;
        }

        #endregion

        #region Handlers

        private void HandleOperatorSelected(int index) { }

        #endregion
    }
}
