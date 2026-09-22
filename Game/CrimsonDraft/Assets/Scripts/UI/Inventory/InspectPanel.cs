#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
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
        [SerializeField] private Image       itemIcon      = null!;
        [SerializeField] private TMP_Text    itemName      = null!;
        [SerializeField] private TMP_Text    itemDescription = null!;
        [SerializeField] private PickupPreviewView? modelPreview;

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

        public bool IsOpen { get; private set; }

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
            OpenInternal(item.Data);
            item.SetInspected(true);
        }

        // For items with no InventoryItemView -- e.g. a permanently-equipped melee weapon,
        // which never enters the spatial inventory grid.
        public void Open(ItemData data)
        {
            this.currentItem = null;
            OpenInternal(data);
        }

        void OpenInternal(ItemData data)
        {
            this.currentItemData = data;

            if (data.PreviewModel != null && this.modelPreview != null)
            {
                this.itemIcon.enabled = false;
                this.modelPreview.Show(data);
            }
            else
            {
                this.modelPreview?.Hide();
                this.itemIcon.sprite  = data.Icon;
                this.itemIcon.enabled = data.Icon != null;
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
            Show();
            this.sfx?.PlayDecide(gameObject);
        }

        public void Close(string? selectItemId = null)
        {
            if (!IsOpen) return;
            IsOpen = false;
            Hide();
            this.modelPreview?.Hide();
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
            if (this.inventoryService.TryRemoveItem(prompt.RequiredItem!.ItemId))
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
            string rewardName = reward.Grant(MakeRewardContext(this.currentItemData));
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

        void Show()
        {
            this.canvasGroup.alpha          = 1f;
            this.canvasGroup.interactable   = true;
            this.canvasGroup.blocksRaycasts = true;
        }

        void Hide()
        {
            this.canvasGroup.alpha          = 0f;
            this.canvasGroup.interactable   = false;
            this.canvasGroup.blocksRaycasts = false;
        }
    }
}
