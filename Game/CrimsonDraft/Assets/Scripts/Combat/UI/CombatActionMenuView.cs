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

        private VisualElement itemFire = null!;
        private VisualElement itemClose = null!;
        private Label selectorFire = null!;
        private Label selectorClose = null!;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;

            this.itemFire  = root.Q("action-item-fire");
            this.itemClose = root.Q("action-item-close");
            this.selectorFire  = root.Q<Label>("selector-fire");
            this.selectorClose = root.Q<Label>("selector-close");

            this.itemFire.focusable  = true;
            this.itemClose.focusable = true;

            this.itemFire.RegisterCallback<FocusInEvent>(this.OnFireFocused);
            this.itemFire.RegisterCallback<FocusOutEvent>(this.OnFireBlurred);
            this.itemClose.RegisterCallback<FocusInEvent>(this.OnCloseFocused);
            this.itemClose.RegisterCallback<FocusOutEvent>(this.OnCloseBlurred);

            this.itemFire.RegisterCallback<NavigationSubmitEvent>(this.OnFireSubmit);
            this.itemClose.RegisterCallback<NavigationSubmitEvent>(this.OnCloseSubmit);

            this.itemFire.Focus();
        }

        private void OnDisable()
        {
            this.itemFire.UnregisterCallback<FocusInEvent>(this.OnFireFocused);
            this.itemFire.UnregisterCallback<FocusOutEvent>(this.OnFireBlurred);
            this.itemClose.UnregisterCallback<FocusInEvent>(this.OnCloseFocused);
            this.itemClose.UnregisterCallback<FocusOutEvent>(this.OnCloseBlurred);

            this.itemFire.UnregisterCallback<NavigationSubmitEvent>(this.OnFireSubmit);
            this.itemClose.UnregisterCallback<NavigationSubmitEvent>(this.OnCloseSubmit);
        }

        private void OnFireFocused(FocusInEvent _)   => this.selectorFire.style.visibility  = Visibility.Visible;
        private void OnFireBlurred(FocusOutEvent _)  => this.selectorFire.style.visibility  = Visibility.Hidden;
        private void OnCloseFocused(FocusInEvent _)  => this.selectorClose.style.visibility = Visibility.Visible;
        private void OnCloseBlurred(FocusOutEvent _) => this.selectorClose.style.visibility = Visibility.Hidden;

        private void OnFireSubmit(NavigationSubmitEvent _)  => this.OnDisparar?.Invoke();
        private void OnCloseSubmit(NavigationSubmitEvent _) => this.OnCerrar?.Invoke();
    }
}
