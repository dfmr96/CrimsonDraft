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
    /// showing the live letter in CODE's current slot. The next empty slot blinks a "-"
    /// caret, like a text field waiting for input. 3s of inactivity locks the letter,
    /// relights START, and moves the caret to the next of the three required letters.
    /// Once all three are locked, Output (SEND) publishes the code via
    /// BeeperSignalRegistry/BeeperSignalSentEvent for any level zone to read and flashes the
    /// result LED red/green. CODE keeps showing the sent letters — and Output is locked out —
    /// until that LED finishes its cycle, only then is CODE cleared. Switching away from the
    /// BEEPER tab or closing the inventory always wipes CODE immediately regardless, so it
    /// never reopens with stale letters.</summary>
    public sealed class BeeperTabController : MonoBehaviour
    {
        [SerializeField] private Transform     ledRoot      = null!; // parent of all LED-<Letter> children
        [SerializeField] private Image         startLed     = null!;
        [SerializeField] private Image         inputButton  = null!;
        [SerializeField] private Image         outputButton = null!;
        [SerializeField] private RectTransform selectorRect = null!; // moves between inputButton/outputButton
        [SerializeField] private TMP_Text[]    codeLetterSlots = null!; // one slot per required letter
        [SerializeField] private Image         chargeFill   = null!; // fills 0→1 over holdThreshold while holding Input
        [SerializeField] private Image         timeFill     = null!; // drains 1→0 over letterTimeout while a letter is active
        [SerializeField] private Image?        signalLed;    // between CODE and Output; flashes the SEND result. Optional.

        [SerializeField] private float cursorBlinkInterval = 0.5f; // blink rate of the "-" placeholder shown in the next empty slot

        [SerializeField] private Color signalReceivedColor    = Color.green;
        [SerializeField] private Color signalNotReceivedColor = Color.red;
        [SerializeField] private float signalLedDuration      = 2f; // seconds visible after SEND before fading back to alpha 0

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

        private char? liveLetter;
        private float cursorBlinkTimer;
        private bool  cursorVisible = true;

        private float signalLedHideTime = -1f; // Time.unscaledTime at which to hide the LED; -1 = not showing

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
            HideSignalLed();

            if (this.inputService == null || this.inputBound) return;
            this.inputService.InventoryConfirm.started  += OnPressStarted;
            this.inputService.InventoryConfirm.canceled += OnPressReleased;
            this.inputBound = true;
        }

        void OnDisable()
        {
            if (this.inputBound && this.inputService != null)
            {
                this.inputService.InventoryConfirm.started  -= OnPressStarted;
                this.inputService.InventoryConfirm.canceled -= OnPressReleased;
                this.inputBound = false;
            }

            // Security: switching away from BEEPER (tab change or closing the inventory)
            // always wipes whatever was typed or just sent — it must never come back with
            // stale letters, even if the result LED was still showing.
            ResetState();
            HideSignalLed();
        }

        void Update()
        {
            if (this.tabManager != null && this.tabManager.IsTabBarActive) return;

            HandleNavigate();
            UpdateChargeFill();
            UpdateCursorBlink();

            if (this.signalLedHideTime >= 0f && Time.unscaledTime >= this.signalLedHideTime)
            {
                HideSignalLed();
                ResetState(); // only safe to clear CODE once the player has seen the result
            }

            if (this.activeLetter != '\0')
            {
                float remaining01 = Mathf.Clamp01(1f - (Time.unscaledTime - this.lastInputTime) / this.letterTimeout);
                SetTimeFill(remaining01);

                if (remaining01 <= 0f)
                    LockCurrentLetter();
            }
        }

        // Blinks the "-" placeholder shown in the next empty CODE slot, like a text-input caret.
        void UpdateCursorBlink()
        {
            this.cursorBlinkTimer += Time.unscaledDeltaTime;
            if (this.cursorBlinkTimer < this.cursorBlinkInterval) return;

            this.cursorBlinkTimer -= this.cursorBlinkInterval;
            this.cursorVisible = !this.cursorVisible;

            if (!this.liveLetter.HasValue)
                RenderCodeSlots();
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
            if (this.signalLedHideTime >= 0f)
            {
                // Still showing the result of the last send — CODE stays put until it clears.
                this.sfx?.PlayInvalidAction(gameObject);
                return;
            }

            if (this.decoder.Word.Count < this.requiredLetters)
            {
                this.sfx?.PlayInvalidAction(gameObject);
                return;
            }

            string code = this.decoder.GetWord();
            this.beeperSignalRegistry.SetSignal(code);
            this.beeperSignalPublisher.Publish(new BeeperSignalSentEvent { Code = code });

            // MessagePipe publishes synchronously, so any receiver that accepted the code has
            // already had the chance to call BeeperSignalRegistry.ClearSignal() by the time
            // Publish() returns — that's how we tell "someone caught it" from "nobody did".
            bool received = this.beeperSignalRegistry.CurrentSignal == null;
            ShowSignalLed(received);

            // CODE is left showing the sent letters — Update() clears it once the LED goes out.
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

        // Flashes the LED red/green for signalLedDuration seconds, then fades back to alpha 0.
        void ShowSignalLed(bool received)
        {
            if (this.signalLed == null) return;

            Color color = received ? this.signalReceivedColor : this.signalNotReceivedColor;
            color.a = 1f;
            this.signalLed.color = color;

            this.signalLedHideTime = Time.unscaledTime + this.signalLedDuration;
        }

        void HideSignalLed()
        {
            this.signalLedHideTime = -1f;

            if (this.signalLed == null) return;

            Color color = this.signalLed.color;
            color.a = 0f;
            this.signalLed.color = color;
        }

        void RefreshCodeText(char? newLiveLetter)
        {
            this.liveLetter = newLiveLetter;

            // A fresh letter just started/locked — reset the blink so the cursor is always
            // visible the instant it appears, instead of picking up mid-blink.
            this.cursorBlinkTimer = 0f;
            this.cursorVisible    = true;

            RenderCodeSlots();
        }

        void RenderCodeSlots()
        {
            string confirmed = this.decoder.GetWord();

            for (int i = 0; i < this.codeLetterSlots.Length; i++)
            {
                TMP_Text slot = this.codeLetterSlots[i];
                if (slot == null) continue;

                if (i < confirmed.Length)
                    slot.text = confirmed[i].ToString();
                else if (i > confirmed.Length)
                    slot.text = string.Empty;
                else // i == confirmed.Length: the slot currently being written
                    slot.text = this.liveLetter.HasValue
                        ? this.liveLetter.Value.ToString()
                        : (this.cursorVisible ? "-" : string.Empty);
            }
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
