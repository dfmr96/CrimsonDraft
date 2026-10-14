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
    /// start button (top right), the Classic/Modern row and the Keyboard/Gamepad row, in the order
    /// they appear on screen; Left/Right moves the cursor inside a row; Confirm applies it or
    /// starts the game), driven through the shared
    /// <see cref="ControlOptionsController"/> placed on the New Game canvas.
    /// </summary>
    public sealed class NewGameNavigator : MonoBehaviour
    {
        [SerializeField] private ControlOptionsController panel = null!;

        private IInputService   inputService = null!;
        private MainMenuSfxData sfx          = null!;
        // Channel ids of the panel (0 = scheme row, 1 = device row, 2 = start button) listed
        // top to bottom as laid out on the canvas, so Up/Down follows what the player sees.
        private static readonly int[] OrderWithStart    = { 2, 0, 1 };
        private static readonly int[] OrderWithoutStart = { 0, 1 };

        private bool isOpen;
        private int  position;

        private int[] Order => this.panel.ChannelCount > 2 ? OrderWithStart : OrderWithoutStart;
        private int  Channel => this.Order[this.position];

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
            this.isOpen   = true;
            this.position = System.Array.IndexOf(this.Order, 0); // start on the Classic/Modern row
            this.panel.ShowOutline(this.Channel);
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
                int next = Mathf.Clamp(this.position + vertical, 0, this.Order.Length - 1);
                if (next == this.position) return;
                this.position = next;
                this.sfx.PlayCursor(gameObject);
                this.panel.ShowOutline(this.Channel);
                return;
            }

            int horizontal = direction.x > 0.5f ? 1 : direction.x < -0.5f ? -1 : 0;
            if (horizontal != 0) this.panel.Adjust(this.Channel, horizontal);
        }

        private void OnConfirm(InputAction.CallbackContext _)
        {
            if (this.isOpen) this.panel.Confirm(this.Channel);
        }
    }
}
