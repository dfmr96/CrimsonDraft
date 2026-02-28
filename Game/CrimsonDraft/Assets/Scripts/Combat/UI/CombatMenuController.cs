#nullable enable

using System;
using UnityEngine.Scripting;
using VContainer.Unity;

namespace CrimsonDraft.Combat
{
    public sealed class CombatMenuController : IInitializable, IDisposable
    {
        #region Dependency Injection

        private readonly ICombatActionMenuView menuView;
        private readonly IQTEView qteView;

        [Preserve]
        public CombatMenuController(ICombatActionMenuView menuView, IQTEView qteView)
        {
            this.menuView = menuView;
            this.qteView = qteView;
        }

        #endregion

        #region IInitializable

        void IInitializable.Initialize()
        {
            this.menuView.OnDisparar += this.HandleDisparar;
            this.menuView.OnCerrar += this.HandleCerrar;
        }

        #endregion

        #region IDisposable

        void IDisposable.Dispose()
        {
            this.menuView.OnDisparar -= this.HandleDisparar;
            this.menuView.OnCerrar -= this.HandleCerrar;
        }

        #endregion

        #region Handlers

        private void HandleDisparar() => this.qteView.Show();
        private void HandleCerrar() => this.qteView.Hide();

        #endregion
    }
}
