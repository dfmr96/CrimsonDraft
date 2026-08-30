#nullable enable

using CrimsonDraft.Infrastructure.Input;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace CrimsonDraft.UI.MainMenu
{
    /// <summary>
    /// Owns navigation for the whole Options screen: switching between the General/Sound tab
    /// groups, the two physical push-button cubes that pick the active tab, and handing
    /// Up/Down/Left/Right off to whichever tab's <see cref="IOptionsChannelPanel"/> is active
    /// (<see cref="GeneralMenuController"/> or <see cref="OptionsMenuController"/>). The button
    /// row sits as one extra slot past the end of the active tab's content list, so going down
    /// from the last item -- or up (wrapping) from the first -- lands on it; from the button row,
    /// Up/Down goes back into the content list.
    /// </summary>
    public sealed class OptionsTabController : MonoBehaviour
    {
        [System.Serializable]
        private sealed class TabButton
        {
            [SerializeField] public Transform  cube    = null!;
            [SerializeField] public GameObject outline = null!;

            [System.NonSerialized] public Vector3 raisedLocalPosition;
            [System.NonSerialized] public Vector3 pressedLocalPosition;
        }

        [Header("Tabs (0 = General, 1 = Sound)")]
        [SerializeField] private GameObject generalGroup = null!;
        [SerializeField] private GameObject soundGroup   = null!;
        [Tooltip("Perillas físicas de cada pestaña -- ambas ocupan el mismo lugar en el radio, solo una está activa a la vez.")]
        [SerializeField] private GameObject generalKnobsGroup = null!;
        [SerializeField] private GameObject soundKnobsGroup   = null!;
        [SerializeField] private GeneralMenuController generalPanel = null!;
        [SerializeField] private OptionsMenuController  soundPanel   = null!;

        [Header("Tab buttons (0 = General, 1 = Sound)")]
        [SerializeField] private TabButton generalButton = null!;
        [SerializeField] private TabButton soundButton   = null!;

        [Header("Button press")]
        [Tooltip("Eje local propio del cubo (antes de su rotación) hacia donde se hunde al quedar seleccionado -- hacia abajo en Y, no hacia adentro.")]
        [SerializeField] private Vector3 pressLocalAxis = Vector3.down;
        [Tooltip("Desplazamiento en unidades de MUNDO (Translate usa Space.Self, que rota pero no escala) -- esta escena es minúscula, así que valores como 0.3 mandarían el botón lejísimos.")]
        [SerializeField] private float   pressDepth     = 0.01f;
        [SerializeField] private float   pressDuration  = 0.08f;
        [SerializeField] private Ease    pressEase      = Ease.OutQuad;

        [Header("Hold-to-repeat (Izquierda/Derecha sobre un canal de sonido)")]
        [SerializeField] private float initialRepeatDelay = 0.35f;
        [SerializeField] private float repeatInterval     = 0.08f;

        private IInputService        inputService = null!;
        private TabButton[]          buttons      = null!;
        private GameObject[]         groups       = null!;
        private GameObject[]         knobGroups   = null!;
        private IOptionsChannelPanel[] panels     = null!;

        private bool isOpen;
        private int  activeTab;
        private bool onButtonRow;
        private int  contentIndex;
        private int  buttonCursor;

        private int   heldHorizontalDirection;
        private float horizontalRepeatTimer;

        [Inject]
        public void Construct(IInputService inputService)
        {
            this.inputService = inputService;
            this.inputService.UINavigate.performed += OnNavigate;
            this.inputService.UIConfirm.performed  += OnConfirm;
        }

        private void Awake()
        {
            this.buttons    = new[] { this.generalButton, this.soundButton };
            this.groups     = new[] { this.generalGroup, this.soundGroup };
            this.knobGroups = new[] { this.generalKnobsGroup, this.soundKnobsGroup };
            this.panels     = new IOptionsChannelPanel[] { this.generalPanel, this.soundPanel };

            foreach (var button in this.buttons)
            {
                button.raisedLocalPosition = button.cube.localPosition;
                button.cube.Translate(this.pressLocalAxis.normalized * this.pressDepth, Space.Self);
                button.pressedLocalPosition = button.cube.localPosition;
                button.cube.localPosition   = button.raisedLocalPosition;
                button.outline.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (this.inputService == null) return;
            this.inputService.UINavigate.performed -= OnNavigate;
            this.inputService.UIConfirm.performed  -= OnConfirm;
        }

        private void OnDisable()
        {
            if (this.buttons == null) return;
            foreach (var button in this.buttons)
                DOTween.Kill(button.cube);
        }

        public void Open()
        {
            this.isOpen                  = true;
            this.activeTab               = 0;
            this.onButtonRow             = true;
            this.buttonCursor            = 0;
            this.heldHorizontalDirection = 0;

            this.generalGroup.SetActive(true);
            this.soundGroup.SetActive(false);
            this.generalKnobsGroup.SetActive(true);
            this.soundKnobsGroup.SetActive(false);

            SetPressedVisual(this.buttons[0], true);
            SetPressedVisual(this.buttons[1], false);

            this.buttons[0].outline.SetActive(true);
            this.buttons[1].outline.SetActive(false);
        }

        public void Close()
        {
            this.isOpen                  = false;
            this.heldHorizontalDirection = 0;

            foreach (var panel in this.panels)
                panel.HideOutlines();
            foreach (var knobGroup in this.knobGroups)
                knobGroup.SetActive(false);
            foreach (var button in this.buttons)
            {
                DOTween.Kill(button.cube);
                button.outline.SetActive(false);
            }
        }

        private void Update()
        {
            if (!this.isOpen || this.onButtonRow || this.heldHorizontalDirection == 0) return;

            float x = this.inputService.UINavigate.ReadValue<Vector2>().x;
            if (Mathf.Abs(x) < 0.5f || (int)Mathf.Sign(x) != this.heldHorizontalDirection)
            {
                this.heldHorizontalDirection = 0;
                return;
            }

            this.horizontalRepeatTimer -= Time.unscaledDeltaTime;
            if (this.horizontalRepeatTimer > 0f) return;

            this.panels[this.activeTab].Adjust(this.contentIndex, this.heldHorizontalDirection);
            this.horizontalRepeatTimer = this.repeatInterval;
        }

        private void OnNavigate(InputAction.CallbackContext ctx)
        {
            if (!this.isOpen) return;

            var direction = ctx.ReadValue<Vector2>();

            int verticalDelta = direction.y > 0.5f ? -1 : direction.y < -0.5f ? 1 : 0;
            if (verticalDelta != 0)
            {
                HandleVertical(verticalDelta);
                return;
            }

            int horizontalDelta = direction.x > 0.5f ? 1 : direction.x < -0.5f ? -1 : 0;
            if (horizontalDelta != 0)
                HandleHorizontal(horizontalDelta);
            else
                this.heldHorizontalDirection = 0;
        }

        private void HandleVertical(int delta)
        {
            int contentCount = this.panels[this.activeTab].ChannelCount;
            int totalSlots   = contentCount + 1; // +1 = the button row, shared by both tabs.
            int currentSlot  = this.onButtonRow ? contentCount : this.contentIndex;
            int nextSlot     = (currentSlot + delta + totalSlots) % totalSlots;

            this.heldHorizontalDirection = 0;

            if (nextSlot == contentCount)
            {
                if (!this.onButtonRow) this.panels[this.activeTab].HideOutlines();
                this.onButtonRow  = true;
                this.buttonCursor = this.activeTab;
                this.buttons[this.buttonCursor].outline.SetActive(true);
                this.buttons[1 - this.buttonCursor].outline.SetActive(false);
            }
            else
            {
                if (this.onButtonRow)
                {
                    this.buttons[this.buttonCursor].outline.SetActive(false);
                    this.onButtonRow = false;
                }
                this.contentIndex = nextSlot;
                this.panels[this.activeTab].ShowOutline(this.contentIndex);
            }
        }

        private void HandleHorizontal(int delta)
        {
            if (this.onButtonRow)
            {
                this.buttons[this.buttonCursor].outline.SetActive(false);
                this.buttonCursor = (this.buttonCursor + delta + this.buttons.Length) % this.buttons.Length;
                this.buttons[this.buttonCursor].outline.SetActive(true);
                return;
            }

            this.panels[this.activeTab].Adjust(this.contentIndex, delta);
            this.heldHorizontalDirection = delta;
            this.horizontalRepeatTimer   = this.initialRepeatDelay;
        }

        private void OnConfirm(InputAction.CallbackContext _)
        {
            if (!this.isOpen || !this.onButtonRow) return;
            SwitchTab(this.buttonCursor);
        }

        private void SwitchTab(int tab)
        {
            if (tab == this.activeTab) return;
            this.activeTab = tab;

            this.groups[tab].SetActive(true);
            this.groups[1 - tab].SetActive(false);
            this.knobGroups[tab].SetActive(true);
            this.knobGroups[1 - tab].SetActive(false);

            SetPressedVisual(this.buttons[tab], true);
            SetPressedVisual(this.buttons[1 - tab], false);
        }

        private void SetPressedVisual(TabButton button, bool pressed)
        {
            DOTween.Kill(button.cube);
            Vector3 target = pressed ? button.pressedLocalPosition : button.raisedLocalPosition;
            button.cube
                .DOLocalMove(target, this.pressDuration)
                .SetTarget(button.cube)
                .SetUpdate(true)
                .SetEase(this.pressEase);
        }
    }
}
