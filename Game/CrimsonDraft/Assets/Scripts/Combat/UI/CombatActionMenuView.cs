#nullable enable

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace CrimsonDraft.Combat
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class CombatActionMenuView : MonoBehaviour, ICombatActionMenuView
    {
        public event Action? OnDisparar;
        public event Action? OnCerrar;

        private Button btnDisparar = null!;
        private Button btnCerrar = null!;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            this.btnDisparar = root.Q<Button>("btn-disparar");
            this.btnCerrar = root.Q<Button>("btn-cerrar");

            this.btnDisparar.clicked += this.HandleDisparar;
            this.btnCerrar.clicked += this.HandleCerrar;

            this.btnDisparar.Focus();
        }

        private void OnDisable()
        {
            this.btnDisparar.clicked -= this.HandleDisparar;
            this.btnCerrar.clicked -= this.HandleCerrar;
        }

        private void HandleDisparar() => this.OnDisparar?.Invoke();
        private void HandleCerrar() => this.OnCerrar?.Invoke();
    }
}
