#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using MessagePipe;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using VContainer;
using Yarn.Unity;
using CrimsonDraft.Inventory;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Navigation.Dialogue;
using CrimsonDraft.Navigation.Interactables.UI;

namespace CrimsonDraft.UI
{
    public class InspectPanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CanvasGroup canvasGroup   = null!;
        // Fallback for items with no 3D preview model; optional (a layout can drop it).
        [SerializeField] private Image?      itemIcon;
        [SerializeField] private TMP_Text    itemName      = null!;
        [SerializeField] private TMP_Text    itemDescription = null!;
        [SerializeField] private PickupPreviewView? modelPreview;

        [Header("Pop")]
        [SerializeField] private float popDuration   = 0.45f;
        [Tooltip("Escala inicial cuando no hay icono de origen (con icono arranca con el tamaño del icono).")]
        [SerializeField, Range(0f, 1f)] private float popStartScale = 0.05f;

        [Header("Examine Text")]
        [SerializeField] private float typewriterCharsPerSecond = 40f;

        [Inject] private InventorySfxData sfx   = null!;
        [Inject] private IInputService    input = null!;
        [Inject] private IInventoryService                inventoryService       = null!;
        [Inject] private IInspectDialogueService          inspectDialogueService = null!;
        [Inject] private NoteRegistry                     noteRegistry           = null!;
        [Inject] private IPublisher<NoteCollectedEvent>   notePublisher          = null!;

        private InventoryItemView? currentItem;
        private ItemData?          currentItemData;

        private string pendingExamineText = string.Empty;
        private int    typingGeneration;
        private bool   isTyping;
        private bool   isActivating; // true while rotating to a hotspot's activationTransform before onUsed fires, and through the reward's animation wait
        private HotspotReward? pendingReward; // non-null while a reward's announcement text is shown, awaiting Confirm to acknowledge it
        private ExaminePrompt? pendingPrompt; // non-null while a hotspot's flavor text is shown, awaiting Confirm to advance to its Yes/No prompt
        private bool   skipRequested;
        private int    openedFrame = -1;

        private Tween?  popTween;
        private RectTransform? lastOrigin;
        private readonly Vector3[] originCorners = new Vector3[4];
        private bool    hasOriginCorners;
        private int     visualGeneration;
        private bool    restCached;
        private Vector3 restLocalPosition;
        private Vector3 restLocalScale = Vector3.one;

        public bool IsOpen { get; private set; }

        /// <summary>How long the closing animation takes (same as the opening).</summary>
        public float CloseDuration => this.popDuration;

        // The string carries the itemId of a just-granted reward to select in the grid
        // (see GridCursor.SelectItemById) -- null for an ordinary close.
        public System.Action<string?>? OnClose;

        void Awake()
        {
            if (this.canvasGroup == null)
                this.canvasGroup = GetComponent<CanvasGroup>();

            IsOpen = false;
            Hide();
        }

        void OnEnable()
        {
            this.input.InventoryConfirm.performed += OnConfirmPressed;
        }

        void OnDisable()
        {
            this.input.InventoryConfirm.performed -= OnConfirmPressed;
        }

        void Update()
        {
            // Lock rotation while the examine text is being typed out, while a hotspot's
            // item-use prompt dialogue is running, or while the model is auto-rotating to
            // its activation angle -- otherwise the player could fight that rotation or
            // rotate onto/off a hotspot mid-interaction.
            if (!IsOpen || this.modelPreview == null || this.isTyping
                || this.inspectDialogueService.IsRunning || this.isActivating
                || this.pendingReward != null || this.pendingPrompt != null) return;
            this.modelPreview.SetRotationInput(this.input.InventoryNavigate.ReadValue<Vector2>());
        }

        public void Open(InventoryItemView item)
        {
            this.currentItem = item;
            OpenInternal(item.Data, origin: (RectTransform)item.transform);
            item.SetInspected(true);
        }

        // For items with no InventoryItemView -- e.g. a permanently-equipped melee weapon,
        // which never enters the spatial inventory grid.
        public void Open(ItemData data)
        {
            this.currentItem = null;
            OpenInternal(data);
        }

        /// <summary>
        /// Opens the panel for a world pickup's "take it?" prompt (see <see cref="PickupInventoryPreview"/>):
        /// the item's 3D model and name, with the prompt itself shown by the Inspect dialogue runner in
        /// the description area. Input stays with that prompt, so none of the examine handling applies.
        /// </summary>
        public void OpenForPickup(ItemData data, RectTransform? origin = null)
        {
            this.currentItem = null;
            OpenInternal(data, playSound: false, origin);
        }

        // Counterpart of <see cref="OpenForPickup"/>: no sound, and no OnClose -- the grid cursor
        // never took part in this opening.
        public void ClosePickup()
        {
            if (!IsOpen) return;
            IsOpen = false;
            PlayClose(() => this.modelPreview?.Hide());
        }

        void OpenInternal(ItemData data, bool playSound = true, RectTransform? origin = null)
        {
            this.currentItemData = data;

            if (data.PreviewModel != null && this.modelPreview != null)
            {
                if (this.itemIcon != null) this.itemIcon.enabled = false;
                this.modelPreview.Show(data);
            }
            else
            {
                this.modelPreview?.Hide();
                if (this.itemIcon != null)
                {
                    this.itemIcon.sprite  = data.Icon;
                    this.itemIcon.enabled = data.Icon != null;
                }
            }

            this.itemName.text        = data.DisplayName;
            this.pendingExamineText   = ExtractExamineText(data.ExamineDialogue);
            this.itemDescription.text = string.Empty;
            this.typingGeneration++; // invalidate any in-flight typewriter from a previous item
            this.isTyping             = false;
            this.skipRequested        = false;
            this.pendingReward        = null;
            this.pendingPrompt        = null;
            this.openedFrame          = Time.frameCount;

            IsOpen = true;
            Show(origin);
            if (playSound) this.sfx?.PlayDecide(gameObject);
        }

        public void Close(string? selectItemId = null)
        {
            if (!IsOpen) return;
            IsOpen = false;
            PlayClose(() => this.modelPreview?.Hide());
            this.sfx?.PlayCancel(gameObject);
            OnClose?.Invoke(selectItemId);
        }

        // Extracts the plain text of a Yarn node's lines without running the DialogueRunner --
        // same technique as FilesTabController.OpenNote(), just without the <page> split.
        static string ExtractExamineText(DialogueReference reference)
        {
            if (reference.project == null || string.IsNullOrEmpty(reference.nodeName))
                return string.Empty;

            var lineIds      = reference.project.GetLineIDsForNodes(new[] { reference.nodeName });
            var localization = reference.project.baseLocalization;

            var sb = new StringBuilder();
            foreach (var id in lineIds)
            {
                var text = localization.GetLocalizedString(id);
                if (string.IsNullOrEmpty(text)) continue;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(text);
            }

            return sb.ToString();
        }

        void OnConfirmPressed(InputAction.CallbackContext _)
        {
            if (!IsOpen) return;

            // Ignore the same Confirm press that opened this panel -- ItemContextMenu's
            // ConfirmSelection() -> InspectPanel.Open() and this handler both react to the
            // same InventoryConfirm.performed dispatch within the same frame.
            if (Time.frameCount == this.openedFrame) return;

            // A hotspot's item-use prompt is a separate input context (PickupNavigate/
            // PickupConfirm via SwitchToPickupPrompt()), but guard defensively in case
            // InventoryConfirm is still wired while it's running.
            if (this.inspectDialogueService.IsRunning) return;

            // The dialogue itself has already finished by the time its "Sí" branch's
            // use_required_item command returns (see UseRequiredItem/ActivateHotspot below),
            // but the model may still be auto-rotating to its activation angle.
            if (this.isActivating) return;

            // While typing, Confirm completes the text instantly. Only once finished does
            // Confirm replay it from the start.
            if (this.isTyping) { this.skipRequested = true; return; }

            // The hotspot's flavor text (shown below, on the first press) has finished typing --
            // this Confirm press advances straight to whatever comes next. Deliberately checked
            // before the general clear-gate below: this transition isn't "read text, clear,
            // then decide what's next" -- the flavor text existing IS what led here, so going
            // straight there is one continuous beat instead of an extra empty press.
            if (this.pendingPrompt is { } readyPrompt)
            {
                this.pendingPrompt = null;

                if (readyPrompt.RequiredItem == null)
                {
                    // No item to ask about -- activates unconditionally (e.g. a book that just
                    // opens itself once examined).
                    ActivateHotspot(readyPrompt).Forget();
                    return;
                }

                this.itemDescription.text = string.Empty; // prompt panel shares this text's rect
                this.inspectDialogueService.StartDialogue(
                    readyPrompt.Dialogue.nodeName ?? string.Empty,
                    variables: new Dictionary<string, object>
                    {
                        ["$item_name"] = readyPrompt.RequiredItem.DisplayName
                    },
                    commands: new Dictionary<string, Action>
                    {
                        ["use_required_item"] = () => UseRequiredItem(readyPrompt)
                    });
                return;
            }

            // The reward's announcement text is fully shown and waiting -- same reasoning as
            // pendingPrompt above: this Confirm press hands off to the reward's own
            // OnAcknowledged straight away instead of first requiring an extra empty press.
            if (this.pendingReward is { } readyReward)
            {
                this.pendingReward = null;
                readyReward.OnAcknowledged(MakeRewardContext(null));
                return;
            }

            // Text is fully shown (not blank) -- this press only clears it. Whatever comes
            // next (retyped text, ...) waits for a separate Confirm press once the box is
            // actually empty, below.
            if (!string.IsNullOrEmpty(this.itemDescription.text))
            {
                this.itemDescription.text = string.Empty;
                return;
            }

            // Hotspot items resolve their text fresh on every press (the player may have
            // rotated the model between attempts); items without hotspots keep the text
            // cached at Open() time.
            var resolution = this.modelPreview?.TryGetExamineDialogue(this.inventoryService);

            if (resolution?.Prompt is { } prompt)
            {
                // Always show the hotspot's own examine text first, same as a plain hotspot --
                // the Yes/No prompt only follows once the player confirms again (see the
                // pendingPrompt branch above).
                this.pendingPrompt = prompt;
                TypewriterRoutine(ExtractExamineText(prompt.FlavorDialogue)).Forget();
                return;
            }

            string text = resolution?.Text != null
                ? ExtractExamineText(resolution.Value.Text)
                : this.pendingExamineText;

            TypewriterRoutine(text).Forget();
        }

        // Registered as the "use_required_item" Yarn command for the duration of a
        // hotspot's prompt dialogue (see above) -- every prompt node's "Sí" branch calls
        // this same fixed command name. Only ever wired up when RequiredItem is non-null (see
        // OnConfirmPressed's pendingPrompt branch). Yarn commands here are synchronous (Action,
        // not a YarnTask) so the node completes immediately after this returns; the rotate+
        // onUsed sequence runs separately and isActivating (checked in Update/OnConfirmPressed)
        // covers the gap between dialogue completion and that sequence finishing.
        void UseRequiredItem(ExaminePrompt prompt)
        {
            if (this.inventoryService.TryRemove(prompt.RequiredItem!.ItemId))
                ActivateHotspot(prompt).Forget();
        }

        async UniTaskVoid ActivateHotspot(ExaminePrompt prompt)
        {
            this.isActivating = true;

            if (prompt.ActivationTransform != null && this.modelPreview != null)
                await this.modelPreview.RotateMountPointTo(prompt.ActivationTransform.localRotation);

            prompt.OnUsed?.Invoke();

            // Waits out the reveal animation onUsed just fired, so the reward doesn't appear
            // mid-animation -- still blocking input via isActivating, same as the rotation above.
            if (prompt.RewardAnimationClip != null)
                await UniTask.Delay(
                    TimeSpan.FromSeconds(prompt.RewardAnimationClip.length), ignoreTimeScale: true);

            this.isActivating = false;

            if (prompt.Reward != null)
                GrantReward(prompt.Reward);
        }

        HotspotRewardContext MakeRewardContext(ItemData? consumedItem) =>
            new(this.inventoryService, this.noteRegistry, this.notePublisher, consumedItem, this.Close);

        // Grant() consumes the item currently being inspected -- BEFORE granting the reward, to
        // free up its grid space -- then types out the reward's announcement the same way
        // ordinary examine text is shown. pendingReward gates OnConfirmPressed until the player
        // acknowledges it, at which point reward.OnAcknowledged takes over (closing the panel
        // and reacting however that reward kind needs to -- see HotspotReward).
        void GrantReward(HotspotReward reward)
        {
            var consumedItem = reward.ConsumeInspectedItem ? this.currentItemData : null;
            string rewardName = reward.Grant(MakeRewardContext(consumedItem));
            this.pendingReward = reward;

            // Yarn's compiled line text uses positional placeholders ({0}, {1}, ...) for
            // interpolated expressions, not the literal "{$var}" source syntax -- string.Format
            // fills it in the same way the DialogueRunner would if this line were run live.
            string text = string.Format(ExtractExamineText(reward.AnnouncementDialogue), rewardName);

            TypewriterRoutine(text).Forget();
        }

        async UniTaskVoid TypewriterRoutine(string text)
        {
            int myGeneration = ++this.typingGeneration;
            this.isTyping      = true;
            this.skipRequested = false;

            float charInterval = 1f / Mathf.Max(1f, this.typewriterCharsPerSecond);
            float timer         = 0f;
            int   revealed      = 0;

            this.itemDescription.text = string.Empty;

            while (revealed < text.Length)
            {
                if (!IsOpen || myGeneration != this.typingGeneration) return;

                if (this.skipRequested)
                {
                    revealed = text.Length;
                    break;
                }

                timer += Time.unscaledDeltaTime;
                while (timer >= charInterval && revealed < text.Length)
                {
                    revealed++;
                    timer -= charInterval;
                }

                this.itemDescription.text = text.Substring(0, revealed);
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            this.itemDescription.text = text;
            this.isTyping             = false;
        }

        void Show(RectTransform? origin = null)
        {
            this.canvasGroup.alpha          = 1f;
            this.canvasGroup.interactable   = true;
            this.canvasGroup.blocksRaycasts = true;
            PlayPop(origin);
        }

        // Pops out of the item's icon (or the panel's own centre when it has none) and grows to its
        // authored size. Unscaled time: the inventory runs with Time.timeScale = 0.
        void PlayPop(RectTransform? origin)
        {
            this.visualGeneration++; // a close still running must not hide this opening
            this.popTween?.Kill();
            this.lastOrigin       = origin;
            this.hasOriginCorners = false; // no icon this time: closing shrinks into the centre

            var rect = (RectTransform)transform;
            CachePose(rect);
            ComputeCollapsedPose(rect, origin, out var start, out var startScale);

            rect.localPosition     = start;
            rect.localScale        = startScale;
            this.canvasGroup.alpha = 0f;

            this.popTween = DOVirtual.Float(0f, 1f, this.popDuration, t =>
                {
                    rect.localPosition = Vector3.LerpUnclamped(start, this.restLocalPosition, t);
                    rect.localScale    = Vector3.LerpUnclamped(startScale, this.restLocalScale, t);
                    this.canvasGroup.alpha = Mathf.Clamp01(t * 4f);
                })
                .SetEase(Ease.OutCubic).SetUpdate(true).SetTarget(this);
        }

        // The opening played backwards: the panel shrinks into the same icon along the same line, then
        // <paramref name="onClosed"/> runs (unless something opened the panel again meanwhile).
        void PlayClose(System.Action? onClosed)
        {
            var rect = (RectTransform)transform;
            this.canvasGroup.interactable   = false;
            this.canvasGroup.blocksRaycasts = false;

            if (!this.restCached || !rect.gameObject.activeInHierarchy)
            {
                Hide();
                onClosed?.Invoke();
                return;
            }

            int generation = ++this.visualGeneration;
            this.popTween?.Kill();

            // Whatever pose the opening was in (it may be cut short) is where the closing starts from.
            Vector3 from      = rect.localPosition;
            Vector3 fromScale = rect.localScale;
            float   fromAlpha = this.canvasGroup.alpha;

            // A destroyed icon compares equal to null: the panel then shrinks into its own centre.
            ComputeCollapsedPose(rect, this.lastOrigin, out var end, out var endScale);

            this.popTween = DOVirtual.Float(0f, 1f, this.popDuration, t =>
                {
                    rect.localPosition = Vector3.LerpUnclamped(from, end, t);
                    rect.localScale    = Vector3.LerpUnclamped(fromScale, endScale, t);
                    this.canvasGroup.alpha = fromAlpha * (1f - Mathf.Clamp01((t - 0.75f) * 4f));
                })
                .SetEase(Ease.InCubic).SetUpdate(true).SetTarget(this)
                .OnKill(() =>
                {
                    if (generation != this.visualGeneration) return;
                    Hide();
                    onClosed?.Invoke();
                });
        }

        void CachePose(RectTransform rect)
        {
            if (this.restCached) return;
            this.restLocalPosition = rect.localPosition;
            this.restLocalScale    = rect.localScale;
            this.restCached        = true;
        }

        // Like a window opening from its taskbar icon: the panel starts as the icon's own rect
        // (same centre, same size) and ends as itself. Position and scale share ONE eased
        // progress value, so the centre travels in a straight line while it grows -- with
        // separate eases it would grow first and then slide into place.
        void ComputeCollapsedPose(RectTransform rect, RectTransform? origin, out Vector3 position, out Vector3 scale)
        {
            position = this.restLocalPosition;
            scale    = this.restLocalScale * this.popStartScale;

            // The icon may be gone by the time the panel closes (a ghost that turned into the real item),
            // so its last known corners are kept.
            if (origin != null) origin.GetWorldCorners(this.originCorners);
            else if (!this.hasOriginCorners) return;
            this.hasOriginCorners = true;
            if (rect.parent == null) return;

            var corners = this.originCorners;
            Vector3 a = rect.parent.InverseTransformPoint(corners[0]);
            Vector3 b = rect.parent.InverseTransformPoint(corners[2]);

            position   = (a + b) * 0.5f;
            position.z = this.restLocalPosition.z;

            var size = rect.rect.size;
            float sx = size.x > 0f ? Mathf.Abs(b.x - a.x) / size.x : this.popStartScale;
            float sy = size.y > 0f ? Mathf.Abs(b.y - a.y) / size.y : this.popStartScale;
            scale = new Vector3(
                this.restLocalScale.x * Mathf.Max(sx, 0.01f),
                this.restLocalScale.y * Mathf.Max(sy, 0.01f),
                this.restLocalScale.z);
        }

        void ResetPop()
        {
            this.popTween?.Kill();
            this.popTween = null;
            if (!this.restCached) return;
            var rect = (RectTransform)transform;
            rect.localPosition = this.restLocalPosition;
            rect.localScale    = this.restLocalScale;
        }

        void Hide()
        {
            ResetPop();
            this.canvasGroup.alpha          = 0f;
            this.canvasGroup.interactable   = false;
            this.canvasGroup.blocksRaycasts = false;
        }
    }
}
