#nullable enable

using System.Collections.Generic;
using MessagePipe;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.UI
{
    /// <summary>Drives the BEEPER tab's Morse key: Input (tap=dot, hold=dash) walks the
    /// MorseDecoder tree one node per press, lighting the matching LED-&lt;letter&gt; and
    /// showing the live letter in CODE. 3s of inactivity locks the letter, relights START,
    /// and moves on to the next of the three required letters. Once all three are locked,
    /// Output (SEND) publishes the code via BeeperSignalRegistry/BeeperSignalSentEvent for
    /// any level zone to read, then clears CODE so the player can start a new one.</summary>
    public sealed class BeeperTabController : MonoBehaviour
    {
        [SerializeField] private Transform     ledRoot      = null!; // parent of all LED-<Letter> children
        [SerializeField] private Image         startLed     = null!;
        [SerializeField] private Image         inputButton  = null!;
        [SerializeField] private Image         outputButton = null!;
        [SerializeField] private RectTransform selectorRect = null!; // moves between inputButton/outputButton
        [SerializeField] private TMP_Text      codeText     = null!;
        [SerializeField] private Image         chargeFill   = null!; // fills 0→1 over holdThreshold while holding Input
        [SerializeField] private Image         timeFill     = null!; // drains 1→0 over letterTimeout while a letter is active

        [SerializeField] private Sprite dotOnSprite   = null!;
        [SerializeField] private Sprite dotOffSprite  = null!;
        [SerializeField] private Sprite dashOnSprite  = null!;
        [SerializeField] private Sprite dashOffSprite = null!;

        [SerializeField] private Color startOnColor  = Color.white;
        [SerializeField] private Color startOffColor = new(1f, 1f, 1f, 0.25f);

        [SerializeField] private float selectorPadding = 8f;
        [SerializeField] private float selectedScale    = 1.1f;
        [SerializeField] private float pressedScale     = 0.9f;

        [SerializeField] private float holdThreshold   = 0.3f;
        [SerializeField] private float letterTimeout   = 3f;
        [SerializeField] private int   requiredLetters = 3;

        [Inject] private IInputService                      inputService          = null!;
        [Inject] private TabManager                         tabManager            = null!;
        [Inject] private InventorySfxData                   sfx                   = null!;
        [Inject] private BeeperSignalRegistry               beeperSignalRegistry  = null!;
        [Inject] private IPublisher<BeeperSignalSentEvent>  beeperSignalPublisher = null!;
        private bool inputBound;

        private readonly MorseDecoder      decoder   = new();
        private readonly Dictionary<char, Image> ledLookup = new();

        private int   focusIndex; // 0 = Input, 1 = Output
        private char  activeLetter;
        private bool  isPressing;
        private float pressStartTime;
        private float lastInputTime;
        private Vector2Int lastDir;

        // ── Lifecycle ────────────────────────────────────────────────────────

        void Awake()
        {
            foreach (Transform child in this.ledRoot)
            {
                if (!child.name.StartsWith("LED-", System.StringComparison.Ordinal)) continue;
                if (child.TryGetComponent<Image>(out var image))
                    this.ledLookup[child.name[4]] = image;
            }
        }

        void OnEnable()
        {
            ResetState();

            if (this.inputService == null || this.inputBound) return;
            this.inputService.InventoryConfirm.started  += OnPressStarted;
            this.inputService.InventoryConfirm.canceled += OnPressReleased;
            this.inputBound = true;
        }

        void OnDisable()
        {
            if (!this.inputBound || this.inputService == null) return;
            this.inputService.InventoryConfirm.started  -= OnPressStarted;
            this.inputService.InventoryConfirm.canceled -= OnPressReleased;
            this.inputBound = false;
        }

        void Update()
        {
            if (this.tabManager != null && this.tabManager.IsTabBarActive) return;

            HandleNavigate();
            UpdateChargeFill();

            if (this.activeLetter != '\0')
            {
                float remaining01 = Mathf.Clamp01(1f - (Time.unscaledTime - this.lastInputTime) / this.letterTimeout);
                SetTimeFill(remaining01);

                if (remaining01 <= 0f)
                    LockCurrentLetter();
            }
        }

        // ── Input: Press (dot/dash on Input, no-op on Output) ──────────────────

        void OnPressStarted(InputAction.CallbackContext ctx)
        {
            if (this.tabManager != null && this.tabManager.IsTabBarActive) return;
            this.isPressing     = true;
            this.pressStartTime = Time.unscaledTime;
            ApplyPressedScale();
        }

        void OnPressReleased(InputAction.CallbackContext ctx)
        {
            if (!this.isPressing) return;
            this.isPressing = false;
            RefreshFocusVisuals(); // grow back from the press-squash

            if (this.tabManager != null && this.tabManager.IsTabBarActive) return;

            bool isLong = (Time.unscaledTime - this.pressStartTime) >= this.holdThreshold;

            if (this.focusIndex == 0) HandleInputPress(isLong);
            else                      HandleOutputPress();
        }

        void HandleInputPress(bool isLong)
        {
            if (this.decoder.Word.Count >= this.requiredLetters)
            {
                this.sfx?.PlayInvalidAction(gameObject);
                return;
            }

            char symbol = isLong ? '-' : '.';

            if (!this.decoder.TryPreview(symbol, out char letter))
            {
                this.sfx?.PlayInvalidAction(gameObject);
                return; // dead node (e.g. "..--") — input is ignored entirely
            }

            if (isLong) this.decoder.InputDash();
            else        this.decoder.InputDot();

            SetActiveLed(letter, symbol);
            RefreshCodeText(letter);

            this.lastInputTime = Time.unscaledTime;
            this.sfx?.PlayCursor(gameObject);
        }

        void HandleOutputPress()
        {
            if (this.decoder.Word.Count < this.requiredLetters)
            {
                this.sfx?.PlayInvalidAction(gameObject);
                return;
            }

            string code = this.decoder.GetWord();
            this.beeperSignalRegistry.SetSignal(code);
            this.beeperSignalPublisher.Publish(new BeeperSignalSentEvent { Code = code });

            ResetState(); // clears CODE and lets the player start typing the next code
            this.sfx?.PlayDecide(gameObject);
        }

        // ── Navigation between Input / Output ───────────────────────────────────

        void HandleNavigate()
        {
            Vector2 raw = this.inputService.InventoryNavigate.ReadValue<Vector2>();

            // Input is above Output — only vertical input moves focus, sideways does nothing.
            if (raw.sqrMagnitude < 0.01f || Mathf.Abs(raw.y) < Mathf.Abs(raw.x))
            {
                this.lastDir = Vector2Int.zero;
                return;
            }

            Vector2Int dir = raw.y > 0 ? Vector2Int.up : Vector2Int.down;
            if (dir == this.lastDir) return;
            this.lastDir = dir;

            int next = Mathf.Clamp(this.focusIndex - dir.y, 0, 1); // up → Input(0), down → Output(1)
            if (next == this.focusIndex) return;

            this.focusIndex = next;
            RefreshFocusVisuals();
            this.sfx?.PlayCursor(gameObject);
        }

        // ── Letter lifecycle ─────────────────────────────────────────────────

        void LockCurrentLetter()
        {
            if (this.activeLetter == '\0') return;

            if (this.ledLookup.TryGetValue(this.activeLetter, out var image))
                SetLedOff(this.activeLetter, image);

            this.activeLetter = '\0';
            this.decoder.Confirm();
            SetStartLit(true);
            RefreshCodeText(null);
            SetTimeFill(1f);
        }

        // ── Visuals ──────────────────────────────────────────────────────────

        void SetActiveLed(char letter, char symbol)
        {
            if (this.activeLetter != '\0' && this.ledLookup.TryGetValue(this.activeLetter, out var prevImage))
                SetLedOff(this.activeLetter, prevImage);

            this.activeLetter = letter;
            SetStartLit(false);

            if (this.ledLookup.TryGetValue(letter, out var image))
                image.sprite = symbol == '.' ? this.dotOnSprite : this.dashOnSprite;
        }

        void SetLedOff(char letter, Image image)
        {
            bool isDot = MorseDecoder.TryGetLastSymbol(letter, out char symbol) && symbol == '.';
            image.sprite = isDot ? this.dotOffSprite : this.dashOffSprite;
        }

        void SetStartLit(bool lit)
        {
            if (this.startLed != null)
                this.startLed.color = lit ? this.startOnColor : this.startOffColor;
        }

        void RefreshFocusVisuals()
        {
            RectTransform focused = this.focusIndex == 0 ? this.inputButton.rectTransform : this.outputButton.rectTransform;

            if (this.selectorRect != null)
            {
                this.selectorRect.anchoredPosition = focused.anchoredPosition;
                this.selectorRect.sizeDelta        = focused.sizeDelta + Vector2.one * (this.selectorPadding * 2f);
            }

            this.inputButton.rectTransform.localScale  = Vector3.one * (this.focusIndex == 0 ? this.selectedScale : 1f);
            this.outputButton.rectTransform.localScale = Vector3.one * (this.focusIndex == 1 ? this.selectedScale : 1f);
        }

        // Squashes whichever button is focused while it's held — RefreshFocusVisuals() grows
        // it back (to selectedScale) on release.
        void ApplyPressedScale()
        {
            RectTransform pressed = this.focusIndex == 0 ? this.inputButton.rectTransform : this.outputButton.rectTransform;
            pressed.localScale = Vector3.one * this.pressedScale;
        }

        void UpdateChargeFill()
        {
            if (this.chargeFill == null) return;

            this.chargeFill.fillAmount = (this.isPressing && this.focusIndex == 0)
                ? Mathf.Clamp01((Time.unscaledTime - this.pressStartTime) / this.holdThreshold)
                : 0f;
        }

        void SetTimeFill(float value01)
        {
            if (this.timeFill != null)
                this.timeFill.fillAmount = value01;
        }

        void RefreshCodeText(char? liveLetter)
        {
            string confirmed = this.decoder.GetWord();
            this.codeText.text = liveLetter.HasValue ? confirmed + liveLetter.Value : confirmed;
        }

        void ResetState()
        {
            this.decoder.Reset();
            this.activeLetter = '\0';
            this.isPressing   = false;
            this.focusIndex   = 0;
            this.lastDir      = Vector2Int.zero;

            foreach (var kv in this.ledLookup)
                SetLedOff(kv.Key, kv.Value);

            SetStartLit(true);
            RefreshFocusVisuals();
            RefreshCodeText(null);
            SetTimeFill(1f);
        }
    }
}
