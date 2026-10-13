#nullable enable

using CrimsonDraft.Navigation.UI;
using CrimsonDraft.Infrastructure.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace CrimsonDraft.UI.MainMenu
{
    /// <summary>
    /// Navigation for the New Game canvas. Same controls as Options > Control (Up/Down between the
    /// Classic/Modern row, the Keyboard/Gamepad row and the start button; Left/Right moves the
    /// cursor inside a row; Confirm applies it or starts the game), driven through the shared
    /// <see cref="ControlOptionsController"/> placed on the New Game canvas.
    /// </summary>
    public sealed class NewGameNavigator : MonoBehaviour
    {
        [SerializeField] private ControlOptionsController panel = null!;

        private IInputService   inputService = null!;
        private MainMenuSfxData sfx          = null!;
        private bool isOpen;
        private int  index;

        [Inject]
        public void Construct(IInputService inputService, MainMenuSfxData sfx)
        {
            this.inputService = inputService;
            this.sfx          = sfx;
            this.inputService.UINavigate.performed += OnNavigate;
            this.inputService.UIConfirm.performed  += OnConfirm;
        }

        private void OnDestroy()
        {
            if (this.inputService == null) return;
            this.inputService.UINavigate.performed -= OnNavigate;
            this.inputService.UIConfirm.performed  -= OnConfirm;
        }

        public void Open()
        {
            this.isOpen = true;
            this.index  = 0;
            this.panel.ShowOutline(this.index);
        }

        public void Close()
        {
            this.isOpen = false;
            this.panel.HideOutlines();
        }

        private void OnNavigate(InputAction.CallbackContext ctx)
        {
            if (!this.isOpen) return;
            var direction = ctx.ReadValue<Vector2>();

            int vertical = direction.y > 0.5f ? -1 : direction.y < -0.5f ? 1 : 0;
            if (vertical != 0)
            {
                int next = Mathf.Clamp(this.index + vertical, 0, this.panel.ChannelCount - 1);
                if (next == this.index) return;
                this.index = next;
                this.sfx.PlayCursor(gameObject);
                this.panel.ShowOutline(this.index);
                return;
            }

            int horizontal = direction.x > 0.5f ? 1 : direction.x < -0.5f ? -1 : 0;
            if (horizontal != 0) this.panel.Adjust(this.index, horizontal);
        }

        private void OnConfirm(InputAction.CallbackContext _)
        {
            if (this.isOpen) this.panel.Confirm(this.index);
        }
    }
}
