#nullable enable

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Navigation.Interactables.UI;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class DocumentController : IInitializable, IDisposable
    {
        private const float NavCooldown = 0.35f;

        private readonly IInputService        inputService;
        private readonly InteractionReaderView view;

        private string[] pages = Array.Empty<string>();
        private string   title = string.Empty;
        private int      pageIndex;
        private bool     isOpen;
        private float    lastNavTime = float.MinValue;

        [Preserve]
        public DocumentController(IInputService inputService, InteractionReaderView view)
        {
            this.inputService = inputService;
            this.view         = view;
        }

        void IInitializable.Initialize()
        {
            this.inputService.UIConfirm.performed  += OnConfirm;
            this.inputService.UICancel.performed   += OnCancel;
            this.inputService.UINavigate.performed += OnNavigate;
        }

        public void Open(string docTitle, string[] docPages)
        {
            this.title       = docTitle;
            this.pages       = docPages;
            this.pageIndex   = 0;
            this.isOpen      = true;
            this.lastNavTime = float.MinValue;
            Time.timeScale    = 0f;
            this.inputService.SwitchToUI();
            RefreshView();
        }

        private void OnConfirm(InputAction.CallbackContext _)
        {
            if (!this.isOpen) return;
            AdvanceOrClose();
        }

        private void OnCancel(InputAction.CallbackContext _)
        {
            if (!this.isOpen) return;
            TryRetreat();
        }

        private void OnNavigate(InputAction.CallbackContext ctx)
        {
            if (!this.isOpen) return;
            if (Time.unscaledTime - this.lastNavTime < NavCooldown) return;

            var x = ctx.ReadValue<Vector2>().x;
            if (x > 0.5f)       AdvanceOrClose();
            else if (x < -0.5f) TryRetreat();
            else return;

            this.lastNavTime = Time.unscaledTime;
        }

        private void AdvanceOrClose()
        {
            if (this.pageIndex >= this.pages.Length - 1)
                Close();
            else
                TryAdvance();
        }

        private void TryAdvance()
        {
            if (this.pageIndex >= this.pages.Length - 1) return;
            this.pageIndex++;
            RefreshView();
        }

        private void TryRetreat()
        {
            if (this.pageIndex <= 0) return;
            this.pageIndex--;
            RefreshView();
        }

        private void Close()
        {
            this.isOpen = false;
            this.view.Hide();
            Time.timeScale = 1f;
            this.inputService.SwitchToGameplay();
        }

        private void RefreshView()
        {
            this.view.Show(
                this.title,
                this.pages[this.pageIndex],
                hasPrev: this.pageIndex > 0,
                hasNext: this.pageIndex < this.pages.Length - 1);
        }

        void IDisposable.Dispose()
        {
            this.inputService.UIConfirm.performed  -= OnConfirm;
            this.inputService.UICancel.performed   -= OnCancel;
            this.inputService.UINavigate.performed -= OnNavigate;
        }
    }
}
