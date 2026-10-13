#nullable enable

using System;
using CrimsonDraft.Infrastructure.Input;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.Navigation.UI
{
    /// <summary>
    /// Control tab content. Two rows, each with two options and a highlight bar that slides under
    /// the option the cursor is on: Classic/Modern (channel 0) and Keyboard/Gamepad (channel 1).
    /// Left/Right moves the cursor (the hovered option grows, the bar slides there and turns
    /// green); Confirm applies it and the bar turns white. Whenever the cursor sits on the applied
    /// option the bar is white. An optional third channel (the New Game start button) fires the
    /// button's onClick on Confirm. The description label follows the scheme the cursor is on.
    /// </summary>
    public sealed class ControlOptionsController : MonoBehaviour, IOptionsChannelPanel
    {
        private const int SchemeChannel = 0;
        private const int DeviceChannel = 1;
        private const int StartChannel  = 2;

        [Serializable]
        private sealed class Option
        {
            [SerializeField] public RectTransform root = null!;

            [NonSerialized] public Vector3 baseScale;
        }

        [Serializable]
        private sealed class Row
        {
            [SerializeField] public Option         left  = null!;
            [SerializeField] public Option         right = null!;
            [SerializeField] public RectTransform  bar   = null!;
            [Tooltip("Offset (en espacio del padre) del centro de la barra respecto de la opción izquierda / derecha, para que siga al botón aunque cambie el layout.")]
            [SerializeField] public Vector2        leftBarPosition;
            [SerializeField] public Vector2        rightBarPosition;

            [NonSerialized] public Image barImage = null!;
            [NonSerialized] public int   cursor;
            [NonSerialized] public int   applied;

            public Option Get(int i) => i == 0 ? this.left : this.right;
        }

        [Header("Scheme row (left = Classic, right = Modern)")]
        [SerializeField] private Row scheme = null!;

        [Header("Device row (left = Keyboard, right = Gamepad)")]
        [SerializeField] private Row device = null!;
        [SerializeField] private GameObject keyboardMapping = null!;
        [SerializeField] private GameObject gamepadMapping  = null!;

        [Header("Optional")]
        [Tooltip("Botón de inicio (New Game). Si se asigna, es un tercer canal navegable.")]
        [SerializeField] private Button? startButton;
        [SerializeField] private TextMeshProUGUI? description;
        [SerializeField, TextArea] private string modernDescription  = "Directional movement with natural, camera relative turning.";
        [SerializeField, TextArea] private string classicDescription = "Tank controls, the character rotates in place to turn.";

        [Header("Feel")]
        [Tooltip("Color de la barra mientras el cursor está en una opción todavía no confirmada (el verde de la UI).")]
        [SerializeField] private Color hoverColor     = Color.green;
        [Tooltip("Color de la barra cuando la opción bajo ella es la aplicada.")]
        [SerializeField] private Color confirmedColor = Color.white;
        [SerializeField] private float hoverScale    = 1.15f;
        [SerializeField] private float scaleDuration = 0.1f;
        [SerializeField] private float slideDuration = 0.2f;
        [SerializeField] private Ease  slideEase     = Ease.OutQuad;

        private Row[] rows = null!;
        private int selectedChannel = -1;
        private Vector3 startBaseScale = Vector3.one;

        private IControlSchemeService controlSchemeService = null!;
        private Action onCursorSfx = null!;
        private Action onDecideSfx = null!;

        public int ChannelCount => this.startButton != null ? 3 : 2;

        /// <summary>Wiring by the host (main menu scope, pause menu): the service plus its own SFX.</summary>
        public void Setup(IControlSchemeService controlSchemeService, Action onCursorSfx, Action onDecideSfx)
        {
            this.controlSchemeService = controlSchemeService;
            this.onCursorSfx          = onCursorSfx;
            this.onDecideSfx          = onDecideSfx;
            if (this.rows != null) Refresh(animate: false);
        }

        /// <summary>Snaps the Keyboard/Gamepad row to the device in hand (no sound, no animation).</summary>
        public void SetDevice(bool gamepad)
        {
            this.device.applied = this.device.cursor = gamepad ? 1 : 0;
            MoveBar(this.device, animate: false);
            ApplyMapping();
            RefreshHover();
        }

        private void Awake()
        {
            this.rows = new[] { this.scheme, this.device };
            if (this.startButton != null) this.startBaseScale = this.startButton.transform.localScale;
            foreach (var row in this.rows)
            {
                row.left.baseScale  = row.left.root.localScale;
                row.right.baseScale = row.right.root.localScale;
                row.barImage       = row.bar.GetComponent<Image>();
            }
        }

        private void Start() => Refresh(animate: false);

        private void OnEnable()
        {
            if (this.controlSchemeService != null) Refresh(animate: false);
        }

        private void OnDisable()
        {
            if (this.rows == null) return;
            foreach (var row in this.rows)
            {
                DOTween.Kill(row.left.root);
                DOTween.Kill(row.right.root);
                DOTween.Kill(row.bar);
            }
            if (this.startButton != null) DOTween.Kill(this.startButton.transform);
        }

        public bool IsSelectable(int index) => true;

        public bool RepeatsOnHold(int index) => false;

        public void ShowOutline(int index)
        {
            this.selectedChannel = index;
            RefreshHover();
        }

        public void HideOutlines()
        {
            this.selectedChannel = -1;
            RefreshHover();
        }

        public void Adjust(int index, int direction)
        {
            if (index >= this.rows.Length) return;
            var row = this.rows[index];
            int next = Mathf.Clamp(row.cursor + direction, 0, 1);
            if (next == row.cursor) return;
            row.cursor = next;
            this.onCursorSfx();
            MoveBar(row, animate: true);
            RefreshDescription();
            RefreshHover();
        }

        public void Confirm(int index)
        {
            if (index == StartChannel)
            {
                this.startButton?.onClick.Invoke();
                return;
            }
            var row = this.rows[index];
            if (row.cursor == row.applied) return;
            row.applied = row.cursor;
            this.onDecideSfx();

            if (index == SchemeChannel)
                this.controlSchemeService.SetScheme(row.applied == 0 ? ControlScheme.Classic : ControlScheme.Modern);
            else
                ApplyMapping();

            TintBar(row);
        }

        // Pulls the applied state from the service and snaps everything to it (no animation).
        private void Refresh(bool animate)
        {
            this.scheme.applied = this.controlSchemeService.CurrentScheme == ControlScheme.Classic ? 0 : 1;
            this.scheme.cursor  = this.scheme.applied;
            this.device.cursor  = this.device.applied;
            foreach (var row in this.rows) MoveBar(row, animate);
            ApplyMapping();
            RefreshDescription();
            RefreshHover();
        }

        private void RefreshDescription()
        {
            if (this.description != null)
                this.description.text = this.scheme.cursor == 0 ? this.classicDescription : this.modernDescription;
        }

        private void ApplyMapping()
        {
            bool isKeyboard = this.device.applied == 0;
            if (this.keyboardMapping != null) this.keyboardMapping.SetActive(isKeyboard);
            if (this.gamepadMapping != null)  this.gamepadMapping.SetActive(!isKeyboard);
        }

        private void MoveBar(Row row, bool animate)
        {
            var option = row.cursor == 0 ? row.left : row.right;
            Vector2 offset = row.cursor == 0 ? row.leftBarPosition : row.rightBarPosition;

            // Bar centre = option pivot + offset (both in the shared parent space); localPosition
            // is the bar's pivot, so remove the pivot->centre distance.
            Vector2 centre = (Vector2)option.root.localPosition + offset;
            var target = (Vector3)(centre - row.bar.rect.center);
            target.z = row.bar.localPosition.z;

            DOTween.Kill(row.bar);
            if (animate)
            {
                row.bar.DOLocalMove(target, this.slideDuration)
                    .SetTarget(row.bar)
                    .SetUpdate(true)
                    .SetEase(this.slideEase);
            }
            else
            {
                row.bar.localPosition = target;
            }
            TintBar(row);
        }

        private void TintBar(Row row) =>
            row.barImage.color = row.cursor == row.applied ? this.confirmedColor : this.hoverColor;

        private void RefreshHover()
        {
            for (int r = 0; r < this.rows.Length; r++)
            {
                var row = this.rows[r];
                for (int i = 0; i < 2; i++)
                    ScaleTo(row.Get(i), this.selectedChannel == r && row.cursor == i);
            }

            if (this.startButton != null)
                ScaleTo(this.startButton.transform, this.startBaseScale, this.selectedChannel == StartChannel);
        }

        private void ScaleTo(Option option, bool enlarged) => ScaleTo(option.root, option.baseScale, enlarged);

        private void ScaleTo(Transform target, Vector3 baseScale, bool enlarged)
        {
            DOTween.Kill(target);
            target
                .DOScale(baseScale * (enlarged ? this.hoverScale : 1f), this.scaleDuration)
                .SetTarget(target)
                .SetUpdate(true);
        }
    }
}
