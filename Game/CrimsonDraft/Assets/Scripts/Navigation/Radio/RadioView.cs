#nullable enable

using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;
using UnityEngine.Rendering;
using Yarn.Unity;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.UI;
using CrimsonDraft.Navigation.UI;

namespace CrimsonDraft.Navigation.Radio
{
    /// <summary>
    /// The Radio_UI panel and the Yarn presenter that feeds it. Hidden by default (object inactive,
    /// canvas off); it is an IInitializable so it listens even while inactive. When the inventory opens with a call
    /// pending, the panel appears "armed": the first advance input starts the call, each further
    /// one skips the typing / moves to the next line, cancel backs out to the inventory.
    ///
    /// Line conventions come from Tools/Dialogue/to-yarn.ps1: the speaker is the character id,
    /// "_beat" lines are silences (text = silence | pause | static) and the pilcrow is a newline.
    /// </summary>
    public sealed class RadioView : DialoguePresenterBase, IInitializable, System.IDisposable
    {
        private const string BeatSpeaker = "_beat";
        private const char   LineBreak   = '¶';

        private enum State { Hidden, Armed, Playing }

        [SerializeField] private Canvas       canvas = null!;
        [SerializeField] private RadioCastData cast  = null!;

        [Header("Portraits")]
        [SerializeField] private Image leftPortrait  = null!;
        [SerializeField] private Image rightPortrait = null!;
        [Range(0f, 1f)]
        [SerializeField] private float inactiveBrightness = 0.45f;

        [Header("Display")]
        [Tooltip("Optional frequency readout.")]
        [SerializeField] private TMP_Text? freqLabel;
        [Tooltip("The bars of the radio display, top to bottom.")]
        [SerializeField] private RectTransform[] bars = System.Array.Empty<RectTransform>();

        [Header("Text box")]
        [SerializeField] private TMP_Text   speakerLabel = null!;
        [SerializeField] private TMP_Text   lineLabel    = null!;
        [SerializeField] private GameObject nextArrow    = null!;
        [SerializeField] private float      charsPerSecond = 40f;

        [Header("Look & sound (same as the pause menu / inventory)")]
        [Tooltip("The shared UI Volume (CRT / PSX look) the inventory and pause menu fade in.")]
        [SerializeField] private Volume? inventoryVolume;
        [SerializeField] private float   volumeFadeDuration = 0.3f;
        [SerializeField] private InventorySfxData? sfx;

        [Inject] private IRadioConversationService radio    = null!;
        [Inject] private RadioDialogueService      dialogue = null!;
        [Inject] private IInputService             input    = null!;

        private State    state;
        private string?  currentNode;
        private string   leftId = string.Empty;
        private string[] rightIds = System.Array.Empty<string>();
        private bool     cancelled;
        private bool     typing;
        private bool     skipTyping;
        private bool     advanceRequested;

        // ── Lifecycle ────────────────────────────────────────────────────────

        // VContainer entry point rather than Start(): Radio_UI is normally left inactive in the
        // scene, and an inactive object never runs Start, so it would never hear the inventory open.
        public void Initialize()
        {
            SetVisible(false);
            this.radio.InventoryOpened  += OnInventoryOpened;
            this.radio.InventoryClosed  += OnInventoryClosed;
            this.dialogue.RadioOpened   += OnRadioOpened;
        }

        public void Dispose()
        {
            this.radio.InventoryOpened  -= OnInventoryOpened;
            this.radio.InventoryClosed  -= OnInventoryClosed;
            this.dialogue.RadioOpened   -= OnRadioOpened;
            UnsubscribeInput();
        }

        private void OnInventoryOpened()
        {
            if (this.state != State.Hidden || this.radio.PendingNode == null) return;
            Arm();
        }

        private void OnInventoryClosed()
        {
            // The inventory can't close while the radio owns input, but be safe.
            if (this.state == State.Armed) Disarm(returnInput: false);
        }

        // ── Armed / playing ──────────────────────────────────────────────────

        private void Arm()
        {
            this.state       = State.Armed;
            this.currentNode = this.radio.PendingNode;
            ClearDisplay();
            SetVisible(true);
            VolumeFader.Fade(this.inventoryVolume, true, this.volumeFadeDuration);
            this.sfx?.PlayCursor(this.gameObject);

            this.input.SwitchToDialogue();
            this.input.DialogueAdvanceLine.performed    += OnAdvance;
            this.input.DialogueCancelDialogue.performed += OnCancel;
        }

        private void Disarm(bool returnInput)
        {
            UnsubscribeInput();
            this.state = State.Hidden;
            SetVisible(false);
            if (returnInput) this.input.SwitchToInventory();
        }

        private void UnsubscribeInput()
        {
            if (this.input == null) return;
            this.input.DialogueAdvanceLine.performed    -= OnAdvance;
            this.input.DialogueCancelDialogue.performed -= OnCancel;
        }

        private void OnAdvance(InputAction.CallbackContext _)
        {
            switch (this.state)
            {
                case State.Armed:
                    this.state     = State.Playing;
                    this.cancelled = false;
                    this.sfx?.PlayDecide(this.gameObject);
                    this.dialogue.Play(this.currentNode!, OnCallFinished);
                    break;

                case State.Playing:
                    if (this.typing) this.skipTyping = true;
                    else             this.advanceRequested = true;
                    break;
            }
        }

        private void OnCancel(InputAction.CallbackContext _)
        {
            if (this.state != State.Hidden) this.sfx?.PlayCancel(this.gameObject);

            switch (this.state)
            {
                case State.Armed:
                    Disarm(returnInput: true);
                    break;

                case State.Playing:
                    // Leaves the call unheard so it can be taken again.
                    this.cancelled = true;
                    this.dialogue.Stop();
                    break;
            }
        }

        // Runs after DialogueService has already handed input back to the inventory.
        private void OnCallFinished()
        {
            UnsubscribeInput();
            this.state = State.Hidden;
            SetVisible(false);

            if (!this.cancelled && this.currentNode != null)
                this.radio.MarkHeard(this.currentNode);
        }

        // ── Yarn command: radio_open ─────────────────────────────────────────

        private void OnRadioOpened(string freq, string left, string[] right)
        {
            this.leftId   = left;
            this.rightIds = right;

            if (this.freqLabel != null) this.freqLabel.text = freq;

            this.leftPortrait.enabled  = true;
            this.rightPortrait.enabled = true;
            ApplyPortrait(this.leftPortrait, left);
            ApplyPortrait(this.rightPortrait, right.FirstOrDefault() ?? string.Empty);
            Dim(this.leftPortrait, true);
            Dim(this.rightPortrait, true);
        }

        // ── DialoguePresenterBase ────────────────────────────────────────────

        public override YarnTask OnDialogueStartedAsync() => YarnTask.CompletedTask;

        public override YarnTask OnDialogueCompleteAsync() => YarnTask.CompletedTask;

        public override async YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
        {
            string who  = line.CharacterName ?? string.Empty;
            string text = (line.TextWithoutCharacterName.Text ?? string.Empty).Replace(LineBreak, '\n');

            this.advanceRequested = false;
            this.skipTyping       = false;
            this.nextArrow.SetActive(false);

            if (who == BeatSpeaker)
            {
                this.speakerLabel.text = string.Empty;
                this.lineLabel.text    = string.Empty;
                Dim(this.leftPortrait, true);
                Dim(this.rightPortrait, true);
                IdleBars();
            }
            else
            {
                ShowSpeaker(who);
                await TypeAsync(text, token);
            }

            this.nextArrow.SetActive(true);
            while (!this.advanceRequested && !this.cancelled && !token.IsNextLineRequested)
                await YarnTask.Yield();
            this.nextArrow.SetActive(false);
        }

        private async YarnTask TypeAsync(string text, LineCancellationToken token)
        {
            this.typing         = true;
            this.lineLabel.text = string.Empty;

            float interval = 1f / Mathf.Max(1f, this.charsPerSecond);
            float timer    = 0f;
            int   revealed = 0;

            while (revealed < text.Length && !this.skipTyping && !this.cancelled && !token.IsNextLineRequested)
            {
                // Unscaled: the inventory (and so the radio) runs with timeScale 0.
                timer += Time.unscaledDeltaTime;
                while (timer >= interval && revealed < text.Length)
                {
                    revealed++;
                    timer -= interval;
                    if (!char.IsWhiteSpace(text[revealed - 1])) JitterBars();
                }

                this.lineLabel.text = text.Substring(0, revealed);
                await YarnTask.Yield();
            }

            this.lineLabel.text = text;
            this.typing         = false;
            IdleBars();
        }

        // ── Display helpers ──────────────────────────────────────────────────

        private void ShowSpeaker(string id)
        {
            bool isRight = this.rightIds.Contains(id);
            var  target  = isRight ? this.rightPortrait : this.leftPortrait;

            ApplyPortrait(target, id);
            Dim(this.leftPortrait, isRight);
            Dim(this.rightPortrait, !isRight);

            if (this.cast.TryGet(id, out var entry))
            {
                this.speakerLabel.text  = entry.displayName.ToUpperInvariant();
                this.speakerLabel.color = entry.color;
            }
            else
            {
                this.speakerLabel.text = id.ToUpperInvariant();
            }
        }

        private void ApplyPortrait(Image target, string id)
        {
            if (this.cast.TryGet(id, out var entry) && entry.portrait != null)
                target.sprite = entry.portrait;
        }

        private void Dim(Image portrait, bool inactive)
        {
            float b = this.inactiveBrightness;
            portrait.color = inactive ? new Color(b, b, b, 1f) : Color.white;
        }

        private void ClearDisplay()
        {
            this.speakerLabel.text = string.Empty;
            this.lineLabel.text    = string.Empty;
            this.nextArrow.SetActive(false);
            // The scene's placeholder faces stay hidden until radio_open says who is on the call.
            this.leftPortrait.enabled  = false;
            this.rightPortrait.enabled = false;
            IdleBars();
        }

        private void IdleBars()
        {
            for (int i = 0; i < this.bars.Length; i++)
                this.bars[i].localScale = new Vector3(1f - i * 0.12f, 1f, 1f);
        }

        private void JitterBars()
        {
            foreach (var bar in this.bars)
                bar.localScale = new Vector3(Random.Range(0.15f, 1f), 1f, 1f);
        }

        private void SetVisible(bool visible)
        {
            this.canvas.enabled = visible;
            this.gameObject.SetActive(visible);
        }
    }
}
