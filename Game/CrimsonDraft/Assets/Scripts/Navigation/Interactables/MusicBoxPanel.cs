#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using Yarn.Unity;
using CrimsonDraft.Rendering.Outline;

namespace CrimsonDraft.Navigation.Interactables
{
    // Cursor navigation inside the music box's inspection view. Stops: the 12 melody sockets
    // (groups a-d of 3), the doll's socket and the crank key's socket.
    //
    // Movement is spatial rather than index-based: the sockets are laid out in rows, some of
    // them diagonal, so each input moves to the nearest stop in that direction as seen from the
    // active camera (a grid in effect, without authoring one).
    //
    // Each group owns one "ping" per socket and exactly one of them is lit at a time -- Confirm
    // on a socket lights its ping and turns off the rest of its group, so four pings are
    // always lit (one per group). The picks are the player's melody guess.
    //
    // The doll and the key are placed from the inventory, outside the inspection, through the
    // box's ItemSocketInteractable (requiredItems[keySlotIndex] / [dollSlotIndex]). Both start
    // hidden and appear when their slot is filled. Confirm on the key socket, once the key is
    // in, offers to turn the crank, which checks the melody (onSolved / onWrong).
    public sealed class MusicBoxPanel : MonoBehaviour
    {
        private const float NavCooldown = 0.2f;
        private const string TurnCrankCommand   = "turn_music_box_crank";
        private const string SelectPingCommand  = "select_music_box_ping";

        // Wwise switch groups: MB_Slot1..4, each with Option1..3.
        private const string SwitchGroupPrefix = "MB_Slot";
        private const string SwitchPrefix      = "Option";

        [Serializable]
        private sealed class Group
        {
            public Transform[]  sockets = Array.Empty<Transform>();  // socket_x_1..3 -- navigation targets
            public GameObject[] pings   = Array.Empty<GameObject>(); // Ping_x_1..3 -- same order as sockets
            public DialogueReference[] descriptions = Array.Empty<DialogueReference>(); // engraving text per socket -- same order as sockets
        }

        [SerializeField] private Group[] groups = Array.Empty<Group>();

        [SerializeField] private ItemSocketInteractable itemSocket = null!;
        [SerializeField] private int keySlotIndex  = 0;
        [SerializeField] private int dollSlotIndex = 1;

        [SerializeField] private Transform  keySocket   = null!; // navigation target for the key
        [SerializeField] private GameObject musicalKey  = null!; // shown once the key is placed
        [SerializeField] private Transform  dollSocket  = null!; // navigation target for the doll
        [SerializeField] private GameObject doll        = null!; // shown once the doll is placed

        // Correct socket index (0-based) per group. Order must match the groups array.
        [SerializeField] private int[] solution = { 1, 0, 1, 2 };

        [SerializeField] private DialogueReference emptyDollDialogue    = new();
        [SerializeField] private DialogueReference dollWaitingDialogue  = new();
        [SerializeField] private DialogueReference missingKeyDialogue   = new();
        [SerializeField] private DialogueReference crankReadyDialogue   = new();
        [SerializeField] private DialogueReference crankPromptDialogue  = new();
        [SerializeField] private DialogueReference pingPromptDialogue   = new(); // "Select this engraving?" Yes/No
        [SerializeField] private DialogueReference wrongMelodyDialogue  = new(); // shown after a wrong melody finishes

        // Wwise. The 4 segments are chained inside Play_MusicBox_Melody itself (delays of one bar
        // each); the player's picks are sent beforehand as switches MB_Slot1..4 = Option1..3.
        // Everything is posted on this GameObject so the sound comes from the box.
        [SerializeField] private AK.Wwise.Event playMelodyEvent = new();
        // 3 bars of delay + the last segment (1 bar + 2.5 s tail) = 15.83 s.
        [SerializeField, Min(0f)] private float melodyDuration = 16f;

        // Solved sequence: doll spins during the melody, sinks, then the reward rises.
        [SerializeField] private float dollSpinSpeed = 120f; // degrees / s
        [SerializeField, Min(0f)] private float dollSinkDistance = 0.2f;
        [SerializeField, Min(0.01f)] private float dollSinkDuration = 1.2f;
        [SerializeField] private GameObject rewardPickup = null!; // KI_12_Pickeable, inactive until solved
        [SerializeField, Min(0f)] private float rewardRiseDistance = 0.1f;
        [SerializeField, Min(0.01f)] private float rewardRiseDuration = 1.2f;

        [SerializeField] private UnityEvent onSolved = new();
        [SerializeField] private UnityEvent onWrong  = new();

        private readonly SelectionOutlineHighlight highlight = new();

        private InteractionContext? context;
        private Transform[] stops = Array.Empty<Transform>();
        private int[]  selected = Array.Empty<int>();
        private int    socketCount;
        private bool   keyPlaced;
        private bool   dollPlaced;
        private bool   isActive;
        private bool   isBusy;
        private int    cursor;
        private int    lastDialogueEndFrame = -1;
        private float  lastNavTime = float.MinValue;

        public bool IsSolved { get; private set; }

        private int DollStop => this.socketCount;
        private int KeyStop  => this.socketCount + 1;

        void Awake()
        {
            this.selected = new int[this.groups.Length];

            var list = new List<Transform>();
            foreach (var group in this.groups)
                list.AddRange(group.sockets);
            this.socketCount = list.Count;
            list.Add(this.dollSocket);
            list.Add(this.keySocket);
            this.stops = list.ToArray();

            for (int g = 0; g < this.groups.Length; g++)
                ApplyPings(g);

            this.doll.SetActive(false);
            this.musicalKey.SetActive(false);

            this.itemSocket.SlotFilled += OnSlotFilled;
        }

        void OnDestroy()
        {
            if (this.itemSocket != null) this.itemSocket.SlotFilled -= OnSlotFilled;
            UnsubscribeInput();
        }

        // ── State ──────────────────────────────────────────────────────────

        private void OnSlotFilled(int index, bool animate)
        {
            if (index == this.keySlotIndex)
            {
                this.keyPlaced = true;
                this.musicalKey.SetActive(true);
            }
            else if (index == this.dollSlotIndex)
            {
                this.dollPlaced = true;
                this.doll.SetActive(true);
            }
        }

        // Lights only the selected ping of the group.
        private void ApplyPings(int group)
        {
            var pings = this.groups[group].pings;
            for (int i = 0; i < pings.Length; i++)
                pings[i].SetActive(i == this.selected[group]);
        }

        private bool IsMelodyCorrect()
        {
            if (this.solution.Length != this.selected.Length) return false;
            for (int g = 0; g < this.selected.Length; g++)
                if (this.selected[g] != this.solution[g]) return false;
            return true;
        }

        private void ResolveSocket(int stop, out int group, out int slot)
        {
            group = 0;
            while (stop >= this.groups[group].sockets.Length)
            {
                stop -= this.groups[group].sockets.Length;
                group++;
            }
            slot = stop;
        }

        // ── Activation ─────────────────────────────────────────────────────

        public void Activate(InteractionContext ctx)
        {
            if (this.isActive) return;

            this.context     = ctx;
            this.isActive    = true;
            this.isBusy      = false;
            this.cursor      = 0;
            this.lastNavTime = float.MinValue;

            ctx.InputService.UINavigate.performed += OnNavigate;
            ctx.InputService.UIConfirm.performed  += OnConfirm;
            UpdateHighlight();
        }

        public void Deactivate()
        {
            if (!this.isActive) return;

            this.isActive = false;
            UnsubscribeInput();
            this.highlight.Clear();
            this.context = null;
        }

        private void UnsubscribeInput()
        {
            if (this.context == null) return;
            this.context.InputService.UINavigate.performed -= OnNavigate;
            this.context.InputService.UIConfirm.performed  -= OnConfirm;
        }

        // ── Navigation ─────────────────────────────────────────────────────

        private void UpdateHighlight()
        {
            this.highlight.Show(this.stops[this.cursor]);
        }

        private static Vector2 ScreenPosition(Camera cam, Transform stop)
        {
            var rend  = stop.GetComponentInChildren<Renderer>();
            var world = rend != null ? rend.bounds.center : stop.position;
            return cam.WorldToScreenPoint(world);
        }

        // Nearest stop in the given screen direction: prefers stops close to the input axis
        // (small sideways offset), and falls back to any stop on that side so no stop is ever
        // unreachable.
        private int FindNeighbor(Vector2 dir)
        {
            var cam = Camera.main;
            if (cam == null) return this.cursor;

            var from = ScreenPosition(cam, this.stops[this.cursor]);

            int   best = -1, bestAny = -1;
            float bestScore = float.MaxValue, bestAnyScore = float.MaxValue;

            for (int i = 0; i < this.stops.Length; i++)
            {
                if (i == this.cursor) continue;

                var   delta = ScreenPosition(cam, this.stops[i]) - from;
                float along = Vector2.Dot(delta, dir);
                if (along <= 0f) continue;

                float perp  = Mathf.Abs(delta.x * dir.y - delta.y * dir.x);
                float score = along + perp * 2f;

                if (score < bestAnyScore) { bestAnyScore = score; bestAny = i; }
                if (perp <= along * 1.5f && score < bestScore) { bestScore = score; best = i; }
            }

            return best >= 0 ? best : bestAny >= 0 ? bestAny : this.cursor;
        }

        // ── Input ──────────────────────────────────────────────────────────

        private void OnNavigate(InputAction.CallbackContext ctx)
        {
            if (!this.isActive || this.isBusy) return;
            if (Time.unscaledTime - this.lastNavTime < NavCooldown) return;

            var raw = ctx.ReadValue<Vector2>();
            if (raw.sqrMagnitude < 0.25f) return;

            var dir = Mathf.Abs(raw.x) >= Mathf.Abs(raw.y)
                ? new Vector2(Mathf.Sign(raw.x), 0f)
                : new Vector2(0f, Mathf.Sign(raw.y));

            int next = FindNeighbor(dir);
            this.lastNavTime = Time.unscaledTime;
            if (next == this.cursor) return;

            this.cursor = next;
            UpdateHighlight();
        }

        private void OnConfirm(InputAction.CallbackContext _)
        {
            if (!this.isActive || this.isBusy || this.context == null) return;
            // The Confirm that dismisses a dialogue line also reaches us -- ignore it.
            if (this.lastDialogueEndFrame == Time.frameCount) return;

            if (this.cursor == this.KeyStop)  { ConfirmKey();  return; }
            if (this.cursor == this.DollStop) { ConfirmDoll(); return; }

            ResolveSocket(this.cursor, out int g, out int s);
            ConfirmSocket(g, s);
        }

        // Engraving text first, then "Select this engraving?" -- only Yes lights the ping.
        private void ConfirmSocket(int group, int slot)
        {
            var descriptions = this.groups[group].descriptions;
            if (slot >= descriptions.Length)
            {
                SelectPing(group, slot);
                return;
            }

            this.isBusy = true;
            var ctx = this.context!;
            ctx.DialogueService.StartDialogue(
                descriptions[slot].nodeName ?? "",
                onComplete: () => StartPingPrompt(ctx, group, slot));
        }

        private void StartPingPrompt(InteractionContext ctx, int group, int slot)
        {
            bool select = false;

            ctx.PickupDialogueService.StartDialogue(
                this.pingPromptDialogue.nodeName ?? "",
                onComplete: () =>
                {
                    ResumeAfterDialogue();
                    if (select) SelectPing(group, slot);
                },
                commands: new Dictionary<string, Action>
                {
                    [SelectPingCommand] = () => select = true
                });
        }

        private void SelectPing(int group, int slot)
        {
            this.selected[group] = slot;
            ApplyPings(group);
        }

        private void ConfirmDoll()
        {
            var node = this.dollPlaced ? this.dollWaitingDialogue : this.emptyDollDialogue;
            ShowNotice(node);
        }

        private void ConfirmKey()
        {
            if (!this.keyPlaced)
            {
                ShowNotice(this.missingKeyDialogue);
                return;
            }

            this.isBusy = true;
            var ctx = this.context!;
            ctx.DialogueService.StartDialogue(
                this.crankReadyDialogue.nodeName ?? "",
                onComplete: () => StartCrankPrompt(ctx));
        }

        private void StartCrankPrompt(InteractionContext ctx)
        {
            bool turn = false;

            ctx.PickupDialogueService.StartDialogue(
                this.crankPromptDialogue.nodeName ?? "",
                onComplete: () =>
                {
                    ResumeAfterDialogue();
                    if (turn) TurnCrankAsync().Forget();
                },
                commands: new Dictionary<string, Action>
                {
                    [TurnCrankCommand] = () => turn = true
                });
        }

        // Plays the player's melody and locks everything (cursor, Confirm and the inspection's
        // Back) until it has finished, right or wrong.
        private async UniTaskVoid TurnCrankAsync()
        {
            var ctx = this.context!;
            var ct  = this.GetCancellationTokenOnDestroy();

            this.isBusy = true;
            ctx.InspectionController.ExitLocked = true;
            this.highlight.Clear();

            bool correct = IsMelodyCorrect();

            for (int g = 0; g < this.selected.Length; g++)
                AkSoundEngine.SetSwitch(SwitchGroupPrefix + (g + 1), SwitchPrefix + (this.selected[g] + 1), this.gameObject);

            this.playMelodyEvent.Post(this.gameObject);

            if (correct)
                await SpinDollAsync(this.melodyDuration, ct);
            else
                await UniTask.Delay(TimeSpan.FromSeconds(this.melodyDuration), ignoreTimeScale: true, cancellationToken: ct);

            if (!correct)
            {
                ctx.InspectionController.ExitLocked = false;
                this.onWrong.Invoke();

                // Still busy until the line is dismissed; ResumeAfterDialogue hands the cursor back.
                ctx.DialogueService.StartDialogue(
                    this.wrongMelodyDialogue.nodeName ?? "",
                    onComplete: ResumeAfterDialogue);
                return;
            }

            await SolveSequenceAsync(ct);

            // Hand control back: leave the inspection view; the box can't be used again, the
            // player only has the new pickup left to grab.
            this.IsSolved = true;
            ctx.InspectionController.ExitLocked = false;
            ctx.InspectionController.ExitNow();
            this.onSolved.Invoke();
        }

        private async UniTask SpinDollAsync(float seconds, CancellationToken ct)
        {
            float t = 0f;
            while (t < seconds)
            {
                float dt = Time.unscaledDeltaTime;
                t += dt;
                this.doll.transform.Rotate(Vector3.up, this.dollSpinSpeed * dt, Space.World);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }

        // The doll sinks into the box, then the reward pickup rises out of the same spot.
        private async UniTask SolveSequenceAsync(CancellationToken ct)
        {
            // The box can't be interacted with anymore.
            if (TryGetComponent<Collider>(out var boxCollider))
                boxCollider.enabled = false;

            var dollT     = this.doll.transform;
            var dollFrom  = dollT.position;
            var dollTo    = dollFrom + Vector3.down * this.dollSinkDistance;
            await LerpAsync(this.dollSinkDuration, ct, k => dollT.position = Vector3.Lerp(dollFrom, dollTo, k));
            this.doll.SetActive(false);

            var rewardT  = this.rewardPickup.transform;
            var rewardTo = rewardT.position;
            var rewardFrom = rewardTo + Vector3.down * this.rewardRiseDistance;
            rewardT.position = rewardFrom;
            this.rewardPickup.SetActive(true);
            await LerpAsync(this.rewardRiseDuration, ct, k => rewardT.position = Vector3.Lerp(rewardFrom, rewardTo, k));
            rewardT.position = rewardTo;
        }

        private static async UniTask LerpAsync(float seconds, CancellationToken ct, Action<float> apply)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                apply(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / seconds)));
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            apply(1f);
        }

        private void ShowNotice(DialogueReference node)
        {
            this.isBusy = true;
            this.context!.DialogueService.StartDialogue(
                node.nodeName ?? "",
                onComplete: ResumeAfterDialogue);
        }

        // The dialogue service hands input back to Gameplay when it ends (see
        // JewelryBoxPanel.ResumeAfterDialogue) -- put the UI map back so the cursor keeps
        // working inside the still-open inspection camera.
        private void ResumeAfterDialogue()
        {
            this.lastDialogueEndFrame = Time.frameCount;
            this.isBusy = false;
            if (!this.isActive || this.context == null) return;

            this.context.InputService.SwitchToUI();
            UpdateHighlight();
        }
    }
}
