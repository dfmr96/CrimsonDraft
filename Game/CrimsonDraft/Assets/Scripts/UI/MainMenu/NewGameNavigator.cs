#nullable enable

using CrimsonDraft.Navigation.UI;
using CrimsonDraft.Infrastructure.Input;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace CrimsonDraft.UI.MainMenu
{
    /// <summary>
    /// Navigation for the New Game canvas, in two steps behind one button. Step 1 (Controls) works
    /// exactly like Options > Control (Up/Down between the button (top right), the Classic/Modern
    /// row and the Keyboard/Gamepad row, in the order they appear on screen; Left/Right moves the
    /// cursor inside a row; Confirm applies it), driven through the shared
    /// <see cref="ControlOptionsController"/>. The button reads "Next": confirming it swaps the
    /// canvas to step 2 (Gamma), where Up/Down moves between the gamma bar and the same button,
    /// now reading "Start", and confirming that starts the game. Cancel from step 2 goes back to
    /// step 1 (see <see cref="TryGoBack"/>) before the camera is allowed to travel back.
    ///
    /// Both steps read and write the persistent settings (control scheme, gamma), so whatever was
    /// changed in Options shows up here and vice versa.
    /// </summary>
    public sealed class NewGameNavigator : MonoBehaviour
    {
        private enum Step { Controls, Gamma }

        [SerializeField] private ControlOptionsController panel = null!;
        [Tooltip("Gamma de la pantalla New Game (segundo paso).")]
        [SerializeField] private GeneralMenuController gammaPanel = null!;
        [Tooltip("Contenedor del paso 1 (Controls-Windows).")]
        [SerializeField] private GameObject controlsGroup = null!;
        [Tooltip("Contenedor del paso 2 (Gamma).")]
        [SerializeField] private GameObject gammaGroup = null!;
        [Tooltip("Texto del boton: dice 'Next' en el paso 1 y 'Start' en el paso 2.")]
        [SerializeField] private TextMeshProUGUI buttonLabel = null!;
        [SerializeField] private MainMenuController mainMenu = null!;
        [SerializeField] private string nextText  = "Next";
        [SerializeField] private string startText = "Start";

        private IInputService   inputService = null!;
        private InventorySfxData sfx         = null!;

        // Channel ids of the panel (0 = scheme row, 1 = device row, 2 = start button) listed
        // top to bottom as laid out on the canvas, so Up/Down follows what the player sees.
        private static readonly int[] OrderWithStart    = { 2, 0, 1 };
        private static readonly int[] OrderWithoutStart = { 0, 1 };

        // Step 2 has only two stops: the gamma bar, then the button.
        private const int GammaItem  = 0;
        private const int StartItem  = 1;

        private bool isOpen;
        private Step step;
        private int  position;

        private int[] Order   => this.panel.ChannelCount > 2 ? OrderWithStart : OrderWithoutStart;
        private int   Channel => this.Order[this.position];

        [Inject]
        public void Construct(IInputService inputService, InventorySfxData sfx)
        {
            this.inputService = inputService;
            this.sfx          = sfx;
            this.inputService.UINavigate.performed += OnNavigate;
            this.inputService.UIConfirm.performed  += OnConfirm;
        }

        private void Start()
        {
            // Pointer clicks on the button take the same path as Confirm on its channel.
            this.panel.StartButton?.onClick.AddListener(OnButtonPressed);
        }

        private void OnDestroy()
        {
            this.panel.StartButton?.onClick.RemoveListener(OnButtonPressed);
            if (this.inputService == null) return;
            this.inputService.UINavigate.performed -= OnNavigate;
            this.inputService.UIConfirm.performed  -= OnConfirm;
        }

        public void Open()
        {
            this.isOpen = true;
            ShowStep(Step.Controls);
        }

        public void Close()
        {
            this.isOpen = false;
            this.panel.HideOutlines();
            this.gammaPanel.HideOutlines();
        }

        /// <summary>Cancel pressed while New Game is open. Returns true if it only went back from
        /// Gamma to Controls (the camera stays put), false if the caller should leave the screen.</summary>
        public bool TryGoBack()
        {
            if (!this.isOpen || this.step != Step.Gamma) return false;
            this.sfx.PlayCancel(gameObject);
            ShowStep(Step.Controls);
            return true;
        }

        private void ShowStep(Step next)
        {
            this.step = next;
            this.controlsGroup.SetActive(next == Step.Controls);
            this.gammaGroup.SetActive(next == Step.Gamma);
            this.buttonLabel.text = next == Step.Controls ? this.nextText : this.startText;

            this.panel.HideOutlines();
            this.gammaPanel.HideOutlines();

            if (next == Step.Controls)
            {
                this.position = System.Array.IndexOf(this.Order, 0); // start on the Classic/Modern row
                this.panel.ShowOutline(this.Channel);
            }
            else
            {
                this.position = GammaItem;
                ShowGammaItem();
            }
        }

        private void ShowGammaItem()
        {
            if (this.position == GammaItem)
            {
                this.panel.HideOutlines();
                this.gammaPanel.ShowOutline(0);
            }
            else
            {
                this.gammaPanel.HideOutlines();
                this.panel.ShowOutline(2); // the button's hover cue lives on the controls panel
            }
        }

        private void OnNavigate(InputAction.CallbackContext ctx)
        {
            if (!this.isOpen) return;
            var direction = ctx.ReadValue<Vector2>();

            int vertical = direction.y > 0.5f ? -1 : direction.y < -0.5f ? 1 : 0;
            if (vertical != 0)
            {
                int count = this.step == Step.Controls ? this.Order.Length : 2;
                int next  = Mathf.Clamp(this.position + vertical, 0, count - 1);
                if (next == this.position) return;
                this.position = next;
                this.sfx.PlayCursor(gameObject);

                if (this.step == Step.Controls) this.panel.ShowOutline(this.Channel);
                else ShowGammaItem();
                return;
            }

            int horizontal = direction.x > 0.5f ? 1 : direction.x < -0.5f ? -1 : 0;
            if (horizontal == 0) return;

            if (this.step == Step.Controls) this.panel.Adjust(this.Channel, horizontal);
            else if (this.position == GammaItem) this.gammaPanel.Adjust(0, horizontal);
        }

        private void OnConfirm(InputAction.CallbackContext _)
        {
            if (!this.isOpen) return;

            if (this.step == Step.Controls)
                this.panel.Confirm(this.Channel); // channel 2 invokes the button's onClick -> OnButtonPressed
            else if (this.position == StartItem)
                this.panel.StartButton?.onClick.Invoke();
        }

        private void OnButtonPressed()
        {
            if (!this.isOpen) return;

            if (this.step == Step.Controls)
            {
                this.sfx.PlayDecide(gameObject);
                ShowStep(Step.Gamma);
            }
            else
            {
                this.mainMenu.StartNewGame();
            }
        }
    }
}
