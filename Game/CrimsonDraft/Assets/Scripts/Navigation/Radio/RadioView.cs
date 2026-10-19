#nullable enable

using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;
using UnityEngine.Rendering;
using Yarn.Unity;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Navigation.Enemy;
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
        [Tooltip("Container holding the whole panel. Scaled on Y from 0 to 1 when the radio opens, " +
                 "like the inventory does (the canvas root itself can't be scaled).")]
        [SerializeField] private RectTransform? scaleRoot;
        [SerializeField] private float openDuration = 0.25f;
        [SerializeField] private RadioCastData cast  = null!;

        [Header("Portraits")]
        [SerializeField] private Image leftPortrait  = null!;
        [SerializeField] private Image rightPortrait = null!;
        [Range(0f, 1f)]
        [SerializeField] private float inactiveBrightness = 0.45f;
        [Tooltip("Same page-change glitch the inventory card plays when it switches to the Bio. " +
                 "Plays on a portrait every time someone new takes its place.")]
        [SerializeField] private CardSwitchGlitch? leftGlitch;
        [SerializeField] private CardSwitchGlitch? rightGlitch;

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
        [Tooltip("Plays every time a portrait changes to a different character (Play_UI_Radio_Change).")]
        [SerializeField] private AK.Wwise.Event changeEvent = new();

        [Inject] private IRadioConversationService radio    = null!;
        [Inject] private RadioDialogueService      dialogue = null!;
        [Inject] private IInputService             input    = null!;
        [Inject] private EnemyNavAgent[]           enemies  = null!;

        private enum Side { None, Left, Right }

        private const float StaticBeatSeconds = 0.45f;

        [Header("Talking feedback")]
        [Tooltip("Seconds per flicker step of the speaking face while the line types.")]
        [SerializeField] private float flickerInterval = 0.09f;
        [Tooltip("Brightness the speaking face dips to on the off step.")]
        [Range(0f, 1f)]
        [SerializeField] private float flickerDip = 0.7f;
        [Tooltip("Seconds between new random bar lengths while someone is talking.")]
        [SerializeField] private float barJitterInterval = 0.07f;
        [Tooltip("How fast the bars glide to their new length (higher = snappier).")]
        [SerializeField] private float barSmoothing = 18f;

        private Side     activeSide = Side.None;
        private bool     flickering;
        private float    staticUntil;
        private float    nextBarJitter;
        private float[]  barTarget = System.Array.Empty<float>();

        private Color    leftBase  = Color.white;
        private Color    rightBase = Color.white;
        private string   lastSpeaker = string.Empty;
        private string   leftShown  = string.Empty;
        private string   rightShown = string.Empty;
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
            // The portraits carry the UI tint in their Image colour; dimming scales that colour.
            this.leftBase  = this.leftPortrait.color;
            this.rightBase = this.rightPortrait.color;
            SetVisible(false);
            this.radio.InventoryOpened  += OnInventoryOpened;
            this.radio.InventoryClosed  += OnInventoryClosed;
            this.dialogue.RadioOpened   += OnRadioOpened;
        }

        public void Dispose()
        {
            DOTween.Kill(this);
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
            SetEnemiesSuspended(true);
            ClearDisplay();
            SetVisible(true);

            // Show who is on the call before it starts, the way the tool does.
            if (this.currentNode != null && this.dialogue.TryGetMeta(this.currentNode, out var freq, out var left, out var right))
                OnRadioOpened(freq, left, right);

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
            SetEnemiesSuspended(false);
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
                    // Every advance input gives the UI "decide" click, whether it finishes the
                    // typing or moves on to the next line.
                    this.sfx?.PlayDecide(this.gameObject);
                    if (this.typing) this.skipTyping = true;
                    else             this.advanceRequested = true;
                    break;
            }
        }

        private void OnCancel(InputAction.CallbackContext _)
        {
            // Backing out is only possible before the call starts. Once it is under way it has to
            // be heard to the end (the Playing state ignores cancel on purpose).
            if (this.state != State.Armed) return;

            this.sfx?.PlayCancel(this.gameObject);
            Disarm(returnInput: true);
        }

        // Runs after DialogueService has already handed input back to the inventory.
        private void OnCallFinished()
        {
            UnsubscribeInput();
            this.state = State.Hidden;
            SetVisible(false);
            SetEnemiesSuspended(false);

            if (!this.cancelled && this.currentNode != null)
                this.radio.MarkHeard(this.currentNode);
        }

        // ── Yarn command: radio_open ─────────────────────────────────────────

        private void OnRadioOpened(string freq, string left, string[] right)
        {
            this.leftId   = left;
            this.rightIds = right;

            if (this.freqLabel != null) this.freqLabel.text = freq;

            // Initial faces, no glitch: the right side starts on the first of the group and is
            // swapped (with the glitch) when someone else speaks first.
            this.leftShown  = left;
            this.rightShown = right.FirstOrDefault() ?? string.Empty;
            this.leftPortrait.enabled  = true;
            this.rightPortrait.enabled = true;
            ApplyPortrait(this.leftPortrait, this.leftShown);
            ApplyPortrait(this.rightPortrait, this.rightShown);
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
                this.activeSide = Side.None;
                Dim(this.leftPortrait, true);
                Dim(this.rightPortrait, true);

                // A "static" beat is interference on both faces and on the bars.
                if (text.Trim() == "static") this.staticUntil = Time.unscaledTime + StaticBeatSeconds;
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
                }

                this.lineLabel.text = text.Substring(0, revealed);
                await YarnTask.Yield();
            }

            this.lineLabel.text = text;
            this.typing         = false;
        }

        // ── Display helpers ──────────────────────────────────────────────────

        private void ShowSpeaker(string id)
        {
            bool isRight = this.rightIds.Contains(id);

            // The "change" sound marks every handover of the radio -- to Vulture and back as well,
            // not only when the face on a side is swapped. The very first line has no handover.
            if (this.lastSpeaker.Length > 0 && this.lastSpeaker != id)
                this.changeEvent.Post(this.gameObject);
            this.lastSpeaker = id;

            // Right side rotates through the group, left side holds whoever is on the other end;
            // a change of face on either one plays the card-switch glitch.
            if (isRight)
            {
                if (this.rightShown != id)
                {
                    this.rightShown = id;
                    SwitchPortrait(this.rightGlitch, this.rightPortrait, id);
                }
            }
            else if (this.leftShown != id)
            {
                this.leftShown = id;
                SwitchPortrait(this.leftGlitch, this.leftPortrait, id);
            }

            this.activeSide = isRight ? Side.Right : Side.Left;
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
            if (this.cast.TryGet(id, out var entry) && entry.Portrait != null)
                target.sprite = entry.Portrait;
        }

        // The face is swapped at the peak of the glitch so the change hides behind the distortion.
        private void SwitchPortrait(CardSwitchGlitch? glitch, Image portrait, string id)
        {
            if (glitch != null && portrait.enabled) glitch.Play(() => ApplyPortrait(portrait, id));
            else ApplyPortrait(portrait, id);
        }

        private void Dim(Image portrait, bool inactive) => Tint(portrait, inactive ? this.inactiveBrightness : 1f);

        private void Tint(Image portrait, float brightness)
        {
            var c = portrait == this.leftPortrait ? this.leftBase : this.rightBase;
            portrait.color = new Color(c.r * brightness, c.g * brightness, c.b * brightness, c.a);
        }

        // ── Talking feedback: face flicker + frequency bars ──────────────────

        private void Update()
        {
            if (this.state == State.Hidden) return;

            // The radio opens over the inventory, which pauses the game -- but a call must never
            // leave the world running (enemies could grab the player mid-call), so re-assert it.
            // The inventory restores the time scale itself when it closes.
            if (Time.timeScale != 0f) Time.timeScale = 0f;

            float now = Time.unscaledTime;
            bool talking = this.typing || now < this.staticUntil;

            UpdateBars(talking, Time.unscaledDeltaTime, now);
            UpdateTalkingFlicker(talking, now);
        }

        // The speaking face blinks while the line is being typed, as if it were the voice
        // (the tool does this with a two-step brightness animation).
        private void UpdateTalkingFlicker(bool talking, float now)
        {
            if (!talking)
            {
                if (!this.flickering) return;
                this.flickering = false;
                Dim(this.leftPortrait,  this.activeSide != Side.Left);
                Dim(this.rightPortrait, this.activeSide != Side.Right);
                return;
            }

            this.flickering = true;
            bool on = (int)(now / this.flickerInterval) % 2 == 0;
            float dip = on ? 1f : this.flickerDip;

            // No active speaker (static beat): interference on both faces.
            if (this.activeSide != Side.Right) Tint(this.leftPortrait,  this.activeSide == Side.Left ? dip : this.inactiveBrightness * dip);
            if (this.activeSide != Side.Left)  Tint(this.rightPortrait, this.activeSide == Side.Right ? dip : this.inactiveBrightness * dip);
        }

        // While talking the bars jump to new random lengths a few times a second and glide there
        // (the noise of the channel); otherwise they settle into the idle funnel.
        private void UpdateBars(bool talking, float dt, float now)
        {
            if (this.bars.Length == 0) return;
            if (this.barTarget.Length != this.bars.Length) this.barTarget = new float[this.bars.Length];

            if (!talking) for (int i = 0; i < this.bars.Length; i++) this.barTarget[i] = IdleBarScale(i);
            else if (now >= this.nextBarJitter)
            {
                this.nextBarJitter = now + this.barJitterInterval;
                for (int i = 0; i < this.bars.Length; i++) this.barTarget[i] = Random.Range(0.15f, 1f);
            }

            float k = 1f - Mathf.Exp(-this.barSmoothing * dt);
            for (int i = 0; i < this.bars.Length; i++)
            {
                var s = this.bars[i].localScale;
                s.x = Mathf.Lerp(s.x, this.barTarget[i], k);
                this.bars[i].localScale = s;
            }
        }

        private static float IdleBarScale(int index) => 1f - index * 0.12f;

        private void ClearDisplay()
        {
            this.speakerLabel.text = string.Empty;
            this.lineLabel.text    = string.Empty;
            this.nextArrow.SetActive(false);
            this.activeSide = Side.None;
            this.flickering = false;
            this.staticUntil = 0f;
            this.lastSpeaker = string.Empty;
            // The scene's placeholder faces stay hidden until radio_open says who is on the call.
            this.leftPortrait.enabled  = false;
            this.rightPortrait.enabled = false;
            IdleBars();
        }

        private void IdleBars()
        {
            for (int i = 0; i < this.bars.Length; i++)
                this.bars[i].localScale = new Vector3(IdleBarScale(i), 1f, 1f);
        }

        // Belt and braces next to the paused time scale: enemies keep their own clock
        // (NavigationTimeScale), so they are frozen explicitly for as long as the radio is up,
        // the same way the inspection view does it. A defeated (destroyed) enemy can be null.
        private void SetEnemiesSuspended(bool suspended)
        {
            foreach (var enemy in this.enemies)
                if (enemy != null) enemy.SetInspectionSuspended(suspended);
        }

        private void SetVisible(bool visible)
        {
            this.canvas.enabled = visible;
            this.gameObject.SetActive(visible);

            DOTween.Kill(this);
            if (this.scaleRoot == null) return;

            if (!visible)
            {
                this.scaleRoot.localScale = Vector3.one;
                return;
            }

            // Same entrance as the inventory: squashed flat on Y, then grows to full size.
            // Unscaled, because the inventory (and so the radio) runs with timeScale 0.
            this.scaleRoot.localScale = new Vector3(1f, 0f, 1f);
            this.scaleRoot.DOScaleY(1f, this.openDuration)
                .SetTarget(this)
                .SetUpdate(true)
                .SetEase(Ease.OutCubic);
        }
    }
}
