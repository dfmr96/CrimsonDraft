#nullable enable

using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using Yarn.Unity;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Infrastructure.UI;
using CrimsonDraft.Navigation.Dialogue;

namespace CrimsonDraft.Navigation.Interactables
{
    // Cursor-style navigation across the generator's breaker panel: a top row of switches,
    // further rows below (possibly shorter/jagged), plus one big lever reachable off the
    // right edge of any row -- and back from the lever via Left, mirroring the row/lever
    // handoff MultiGroupPuzzleView already uses for its UI version of this same idea
    // (leverSourceGroup). Moves on UINavigate (arrows/gamepad), same action + cooldown
    // pattern as PuzzleViewController.OnNavigate.
    //
    // Selection is marked by moving whichever target's own renderers onto the "Outline" layer
    // (restoring their original layer once deselected/deactivated) -- OutlineRendererFeature
    // picks that layer up and draws a screen-space edge-detected rim around it, same technique
    // Unity's own editor selection outline uses. That traces the real silhouette from any
    // camera angle and works on compound multi-mesh objects (the lever) exactly as well as
    // single-mesh ones, unlike the KnobOutline inverted-hull technique used for the main-menu
    // knobs, which needs one contiguous mesh per outlined target.
    //
    // Solution: top row (6 switches) must read 43 in binary; the two bottom rows (5 switches
    // each) must sum to 43 -- one switch in the bottom row is permanently "rusted" (always
    // counts as 0, shows rustedFuseDialogue instead of toggling), which is why only the
    // combinations using 12-15 on that row work out (its effective range is 4 bits, not 5).
    // Confirm on the lever plays the handle-pull animation, checks all three rows, and flashes
    // each row green/red. All three correct -> 2s hold, then the inspection view force-closes
    // and the panel locks forever (IsSolved). Any row wrong -> shorter hold, then every switch
    // resets to off and the handle animates back up.
    public sealed class GeneratorSwitchPanel : MonoBehaviour
    {
        private const float NavCooldown = 0.2f;

        private const string OnChildName  = "Switch-on";
        private const string OffChildName = "Switch-off";

        private const int TargetTopValue  = 43;
        private const int TargetBottomSum = 43;

        private const float LeverAnimDuration  = 0.35f;
        private const float SuccessHoldSeconds = 2f;
        private const float FailureHoldSeconds = 1.5f;

        // Handle's local transform when pulled down, measured directly in-editor -- rest state
        // is wherever leverHandle sits authored in the scene (its localPosition/localRotation
        // at Awake), not necessarily zero.
        private static readonly Vector3 LeverPulledLocalPosition = new(-0.54f, 0f, -0.72f);
        private static readonly Quaternion LeverPulledLocalRotation = Quaternion.Euler(0.239f, -68.756f, 0f);

        [SerializeField] private Transform[] topRowSwitches    = Array.Empty<Transform>();
        [SerializeField] private Transform[] midRowSwitches    = Array.Empty<Transform>();
        [SerializeField] private Transform[] bottomRowSwitches = Array.Empty<Transform>();
        [SerializeField] private Transform   lever             = null!;
        // The part that visually moves/rotates when the lever is pulled, between its
        // authored rest transform and LeverPulledLocalPosition/Rotation.
        [SerializeField] private Transform   leverHandle       = null!;

        // One LCD readout per row -- each row's switches double as bits of a binary number
        // (leftmost switch = most significant bit), decoded to decimal on that row's own
        // display whenever a switch in it is toggled.
        [SerializeField] private TextMeshProUGUI topRowDisplay    = null!;
        [SerializeField] private TextMeshProUGUI midRowDisplay    = null!;
        [SerializeField] private TextMeshProUGUI bottomRowDisplay = null!;

        // The top row starts without power (no fuse installed) -- its switches show the
        // parent's own "disabled" model instead of an on/off light, and Confirm on one of them
        // shows missingPowerDialogue instead of toggling. PowerTopRow() (wired to the fuse
        // ItemSocketInteractable's onActivated) flips them over to normal on/off behaviour.
        [SerializeField] private DialogueReference missingPowerDialogue = new();

        // One switch (bottomRowSwitches[rustedCol], row index rustedRow) is permanently
        // disabled -- Confirm on it shows this instead of toggling, and it always counts as 0
        // in that row's binary value regardless of switchOn's stored state.
        [SerializeField] private DialogueReference rustedFuseDialogue = new();
        [SerializeField] private int               rustedRow = 2;
        [SerializeField] private int               rustedCol = 0;

        // Optional -- once inserted, its OnActivated event calls PowerTopRow(). Left unset,
        // the top row just stays disabled forever (fine before the socket exists in-scene).
        [SerializeField] private ItemSocketInteractable? fuseSocket;

        // Shown full-screen (via ScreenFader) once all three rows check out -- same demo-end
        // beat PuzzleInteractable uses elsewhere, restored here since this panel replaced the
        // older electric-box puzzle that used to trigger it.
        [SerializeField, TextArea] private string demoEndMessage = "Hasta aquí llega la demo.\nGracias por jugar.";

        private Transform[][]     rows        = Array.Empty<Transform[]>();
        private TextMeshProUGUI[] rowDisplays = Array.Empty<TextMeshProUGUI>();
        // Per-switch on/off state, same shape as rows.
        private bool[][] switchOn = Array.Empty<bool[]>();
        private bool     topRowPowered;

        private IInputService       inputService        = null!;
        private IDialogueService    dialogueService      = null!;
        private InspectionController inspectionController = null!;
        private ScreenFader          screenFader          = null!;

        private Renderer[] highlightedRenderers      = Array.Empty<Renderer>();
        private int[]      highlightedOriginalLayers = Array.Empty<int>();

        private int   currentRow;
        private int   currentCol;
        private bool  onLever;
        private int   leverReturnRow;
        private bool  isActive;
        private bool  isVerifying;
        private bool  isSolved;
        private float lastNavTime = float.MinValue;

        private Vector3    leverRestLocalPosition;
        private Quaternion leverRestLocalRotation;

        public bool IsSolved => this.isSolved;

        [Inject]
        public void Construct(IInputService inputService, IDialogueService dialogueService, InspectionController inspectionController, ScreenFader screenFader)
        {
            this.inputService         = inputService;
            this.dialogueService      = dialogueService;
            this.inspectionController = inspectionController;
            this.screenFader          = screenFader;
        }

        void Awake()
        {
            this.rows        = new[] { this.topRowSwitches, this.midRowSwitches, this.bottomRowSwitches };
            this.rowDisplays = new[] { this.topRowDisplay, this.midRowDisplay, this.bottomRowDisplay };

            this.switchOn = new bool[this.rows.Length][];
            for (int r = 0; r < this.rows.Length; r++)
            {
                this.switchOn[r] = new bool[this.rows[r].Length];
                for (int c = 0; c < this.rows[r].Length; c++)
                {
                    if (r == 0)                 ApplyDisabledVisual(this.rows[r][c]);
                    else if (IsRusted(r, c))     ApplyDisabledVisual(this.rows[r][c]);
                    else                         ApplySwitchVisual(this.rows[r][c], on: false);
                }
                UpdateRowDisplay(r);
            }

            if (this.fuseSocket != null)
                this.fuseSocket.OnActivated.AddListener(PowerTopRow);

            if (this.leverHandle != null)
            {
                this.leverRestLocalPosition = this.leverHandle.localPosition;
                this.leverRestLocalRotation = this.leverHandle.localRotation;
            }
        }

        private bool IsRusted(int row, int col) => row == this.rustedRow && col == this.rustedCol;

        // Wired from the fuse ItemSocketInteractable's onActivated (UnityEvent, no args --
        // hence a parameterless method rather than taking the row index).
        public void PowerTopRow()
        {
            if (this.topRowPowered) return;
            this.topRowPowered = true;

            foreach (var sw in this.topRowSwitches)
                ApplySwitchVisual(sw, on: false);
            UpdateRowDisplay(0);

            if (this.isActive && !this.onLever && this.currentRow == 0)
                UpdateHighlight();
        }

        public void Activate()
        {
            if (this.isActive || this.isSolved) return;

            this.isActive    = true;
            this.currentRow  = 0;
            this.currentCol  = 0;
            this.onLever     = false;
            this.lastNavTime = float.MinValue;

            this.inputService.UINavigate.performed += OnNavigate;
            this.inputService.UIConfirm.performed  += OnConfirm;
            UpdateHighlight();
        }

        public void Deactivate()
        {
            if (!this.isActive) return;

            this.isActive = false;
            this.inputService.UINavigate.performed -= OnNavigate;
            this.inputService.UIConfirm.performed  -= OnConfirm;
            ClearHighlight();
        }

        private void OnNavigate(InputAction.CallbackContext ctx)
        {
            if (!this.isActive || this.isVerifying) return;
            if (Time.unscaledTime - this.lastNavTime < NavCooldown) return;

            var dir = ctx.ReadValue<Vector2>();

            // If this reads backwards in-game (Right moving the cursor left, etc.), it's
            // because the panel's world-space column order couldn't be verified against the
            // inspect camera without playtesting -- swap the two branches below.
            if      (dir.x >  0.5f) MoveRight();
            else if (dir.x < -0.5f) MoveLeft();
            else if (dir.y >  0.5f) MoveUp();
            else if (dir.y < -0.5f) MoveDown();
            else return;

            this.lastNavTime = Time.unscaledTime;
        }

        private void OnConfirm(InputAction.CallbackContext ctx)
        {
            if (!this.isActive || this.isVerifying) return;

            if (this.onLever)
            {
                PullLever();
                return;
            }

            if (this.currentRow == 0 && !this.topRowPowered)
            {
                ShowMissingPowerPrompt();
                return;
            }

            if (IsRusted(this.currentRow, this.currentCol))
            {
                ShowRustedFusePrompt();
                return;
            }

            bool newState = !this.switchOn[this.currentRow][this.currentCol];
            this.switchOn[this.currentRow][this.currentCol] = newState;
            ApplySwitchVisual(this.rows[this.currentRow][this.currentCol], newState);
            UpdateRowDisplay(this.currentRow);

            // The on/off toggle swaps which child renderer is active, so the highlight (captured
            // against whichever renderer was active when we selected this switch) needs
            // recapturing -- otherwise the outline stays on the now-inactive model and the
            // newly-active one never gets moved onto the Outline layer at all.
            UpdateHighlight();
        }

        private void PullLever()
        {
            PullLeverAsync().Forget();
        }

        private async UniTaskVoid PullLeverAsync()
        {
            this.isVerifying = true;
            await AnimateLeverAsync(down: true);

            bool topOk    = ComputeRowValue(0) == TargetTopValue;
            int  midValue = ComputeRowValue(1);
            int  botValue = ComputeRowValue(2);
            bool bottomOk = midValue + botValue == TargetBottomSum;

            ShowRowFeedback(0, topOk);
            ShowRowFeedback(1, bottomOk);
            ShowRowFeedback(2, bottomOk);

            if (topOk && bottomOk)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(SuccessHoldSeconds), ignoreTimeScale: true);
                this.isSolved = true;
                this.inspectionController.ExitNow();
                // Left verifying + the green flash on -- the panel is locked (Activate() now
                // refuses) and the handle stays pulled down as a visible "already solved" cue.
                this.screenFader.ShowEndScreenAsync(this.demoEndMessage).Forget();
                return;
            }

            await UniTask.Delay(TimeSpan.FromSeconds(FailureHoldSeconds), ignoreTimeScale: true);
            ResetAllSwitches();
            await AnimateLeverAsync(down: false);
            this.isVerifying = false;
        }

        private async UniTask AnimateLeverAsync(bool down)
        {
            if (this.leverHandle == null) return;

            Vector3    fromPos = down ? this.leverRestLocalPosition : LeverPulledLocalPosition;
            Vector3    toPos   = down ? LeverPulledLocalPosition    : this.leverRestLocalPosition;
            Quaternion fromRot = down ? this.leverRestLocalRotation : LeverPulledLocalRotation;
            Quaternion toRot   = down ? LeverPulledLocalRotation    : this.leverRestLocalRotation;

            float t = 0f;
            while (t < LeverAnimDuration)
            {
                t += Time.unscaledDeltaTime;
                float lerp = Mathf.Clamp01(t / LeverAnimDuration);
                this.leverHandle.localPosition = Vector3.Lerp(fromPos, toPos, lerp);
                this.leverHandle.localRotation = Quaternion.Slerp(fromRot, toRot, lerp);
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            this.leverHandle.localPosition = toPos;
            this.leverHandle.localRotation = toRot;
        }

        private void ShowRowFeedback(int row, bool correct)
        {
            var switches = this.rows[row];
            for (int c = 0; c < switches.Length; c++)
            {
                if (IsRusted(row, c)) continue; // stays on its own disabled model
                ApplySwitchVisual(switches[c], on: correct);
            }
        }

        private void ResetAllSwitches()
        {
            for (int r = 0; r < this.rows.Length; r++)
            {
                for (int c = 0; c < this.rows[r].Length; c++)
                {
                    if (IsRusted(r, c)) continue;

                    this.switchOn[r][c] = false;
                    ApplySwitchVisual(this.rows[r][c], on: false);
                }
                UpdateRowDisplay(r);
            }

            if (this.isActive) UpdateHighlight();
        }

        // Reads the row's switches as bits of a binary number -- index 0 (leftmost) is the
        // most significant bit -- ignoring the rusted switch, if any, in that row (it always
        // counts as 0 regardless of its stored state).
        private int ComputeRowValue(int row)
        {
            var switches = this.switchOn[row];
            int value = 0;
            for (int c = 0; c < switches.Length; c++)
            {
                if (IsRusted(row, c)) continue;
                if (switches[c]) value |= 1 << (switches.Length - 1 - c);
            }
            return value;
        }

        // If this reads backwards once the panel is actually laid out, flip the bit weight in
        // ComputeRowValue (use `c` instead of `switches.Length - 1 - c`).
        private void UpdateRowDisplay(int row)
        {
            var display = this.rowDisplays[row];
            if (display == null) return;
            display.text = ComputeRowValue(row).ToString("D2");
        }

        private void ShowMissingPowerPrompt()
        {
            // Same DialogueService the examine text uses -- not PickupDialogueService, no
            // choice needed here, just a notice. DialogueService.OnDialogueComplete() switches
            // input back to Gameplay by default before this callback runs (see
            // GeneratorInteractable for the same gotcha) -- switch back to UI so the cursor
            // keeps responding once the message closes instead of going dead.
            this.dialogueService.StartDialogue(
                this.missingPowerDialogue.nodeName ?? "",
                onComplete: () => this.inputService.SwitchToUI());
        }

        private void ShowRustedFusePrompt()
        {
            this.dialogueService.StartDialogue(
                this.rustedFuseDialogue.nodeName ?? "",
                onComplete: () => this.inputService.SwitchToUI());
        }

        private static void ApplySwitchVisual(Transform switchTransform, bool on)
        {
            // The switch's own parent-level mesh is the "disabled" (fuse-missing) model --
            // it has no on/off state of its own, but it sits right on top of the Switch-on/
            // Switch-off children at the same position, so leaving its Renderer on hides
            // whichever child we just toggled.
            var ownRenderer = switchTransform.GetComponent<Renderer>();
            if (ownRenderer != null) ownRenderer.enabled = false;

            var onChild  = switchTransform.Find(OnChildName);
            var offChild = switchTransform.Find(OffChildName);
            if (onChild  != null) onChild.gameObject.SetActive(on);
            if (offChild != null) offChild.gameObject.SetActive(!on);
        }

        private static void ApplyDisabledVisual(Transform switchTransform)
        {
            var ownRenderer = switchTransform.GetComponent<Renderer>();
            if (ownRenderer != null) ownRenderer.enabled = true;

            var onChild  = switchTransform.Find(OnChildName);
            var offChild = switchTransform.Find(OffChildName);
            if (onChild  != null) onChild.gameObject.SetActive(false);
            if (offChild != null) offChild.gameObject.SetActive(false);
        }

        private void MoveRight()
        {
            if (this.onLever) return;

            var switches = this.rows[this.currentRow];
            if (this.currentCol < switches.Length - 1)
            {
                this.currentCol++;
            }
            else
            {
                this.onLever        = true;
                this.leverReturnRow = this.currentRow;
            }
            UpdateHighlight();
        }

        private void MoveLeft()
        {
            if (this.onLever)
            {
                this.onLever    = false;
                this.currentRow = this.leverReturnRow;
                this.currentCol = this.rows[this.currentRow].Length - 1;
            }
            else if (this.currentCol > 0)
            {
                this.currentCol--;
            }
            UpdateHighlight();
        }

        private void MoveUp()
        {
            if (this.onLever || this.currentRow == 0) return;
            this.currentRow--;
            ClampCol();
            UpdateHighlight();
        }

        private void MoveDown()
        {
            if (this.onLever || this.currentRow == this.rows.Length - 1) return;
            this.currentRow++;
            ClampCol();
            UpdateHighlight();
        }

        private void ClampCol()
        {
            int lastCol = this.rows[this.currentRow].Length - 1;
            if (this.currentCol > lastCol) this.currentCol = lastCol;
        }

        private void UpdateHighlight()
        {
            ClearHighlight();

            Transform target = this.onLever ? this.lever : this.rows[this.currentRow][this.currentCol];
            this.highlightedRenderers     = target.GetComponentsInChildren<Renderer>();
            this.highlightedOriginalLayers = new int[this.highlightedRenderers.Length];

            int outlineLayer = LayerMask.NameToLayer("Outline");
            for (int i = 0; i < this.highlightedRenderers.Length; i++)
            {
                this.highlightedOriginalLayers[i] = this.highlightedRenderers[i].gameObject.layer;
                this.highlightedRenderers[i].gameObject.layer = outlineLayer;
            }
        }

        private void ClearHighlight()
        {
            for (int i = 0; i < this.highlightedRenderers.Length; i++)
                this.highlightedRenderers[i].gameObject.layer = this.highlightedOriginalLayers[i];

            this.highlightedRenderers      = Array.Empty<Renderer>();
            this.highlightedOriginalLayers = Array.Empty<int>();
        }

        void OnDestroy()
        {
            if (this.isActive)
            {
                this.inputService.UINavigate.performed -= OnNavigate;
                this.inputService.UIConfirm.performed  -= OnConfirm;
            }
        }
    }
}
