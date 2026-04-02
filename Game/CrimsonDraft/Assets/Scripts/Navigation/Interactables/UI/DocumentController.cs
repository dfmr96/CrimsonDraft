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
        private readonly IInputService        inputService;
        private readonly InteractionReaderView view;

        private string[] pages = Array.Empty<string>();
        private string   title = string.Empty;
        private int      pageIndex;
        private bool     isOpen;

        [Preserve]
        public DocumentController(IInputService inputService, InteractionReaderView view)
        {
            this.inputService = inputService;
            this.view         = view;
        }

        void IInitializable.Initialize()
        {
            this.inputService.UINavigate.performed += OnNavigate;
            this.inputService.UIBack.performed     += OnBack;
        }

        public void Open(string docTitle, string[] docPages)
        {
            this.title     = docTitle;
            this.pages     = docPages;
            this.pageIndex = 0;
            this.isOpen    = true;
            Time.timeScale  = 0f;
            this.inputService.SwitchToUI();
            RefreshView();
        }

        private void OnNavigate(InputAction.CallbackContext ctx)
        {
            if (!this.isOpen) return;

            var dir = ctx.ReadValue<UnityEngine.Vector2>();
            if (dir.x > 0.5f)
                TryAdvance();
            else if (dir.x < -0.5f)
                TryRetreat();
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

        private void OnBack(InputAction.CallbackContext _)
        {
            if (!this.isOpen) return;
            Close();
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
            this.inputService.UINavigate.performed -= OnNavigate;
            this.inputService.UIBack.performed     -= OnBack;
        }
    }
}
