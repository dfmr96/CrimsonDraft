#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using VContainer;
using Yarn.Unity;
using CrimsonDraft.Inventory;
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
        [Inject] private IInventoryService        inventoryService       = null!;
        [Inject] private IInspectDialogueService  inspectDialogueService = null!;

        private InventoryItemView? currentItem;

        private string pendingExamineText = string.Empty;
        private int    typingGeneration;
        private bool   isTyping;
        private bool   skipRequested;
        private int    openedFrame = -1;

        public bool IsOpen { get; private set; }
        public System.Action? OnClose;

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
            // Lock rotation while the examine text is being typed out, or while a hotspot's
            // item-use prompt dialogue is running -- otherwise the player could rotate
            // onto/off a hotspot mid-interaction.
            if (!IsOpen || this.modelPreview == null || this.isTyping || this.inspectDialogueService.IsRunning) return;
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
            this.openedFrame          = Time.frameCount;

            IsOpen = true;
            Show();
            this.sfx?.PlayDecide(gameObject);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Hide();
            this.modelPreview?.Hide();
            this.sfx?.PlayCancel(gameObject);
            OnClose?.Invoke();
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

            // While typing, Confirm completes the text instantly. Only once finished does
            // Confirm replay it from the start.
            if (this.isTyping) { this.skipRequested = true; return; }

            // Hotspot items resolve their text fresh on every press (the player may have
            // rotated the model between attempts); items without hotspots keep the text
            // cached at Open() time.
            var resolution = this.modelPreview?.TryGetExamineDialogue(this.inventoryService);

            if (resolution?.Prompt is { } prompt)
            {
                this.inspectDialogueService.StartDialogue(
                    prompt.Dialogue.nodeName ?? string.Empty,
                    variables: new Dictionary<string, object>
                    {
                        ["$item_name"] = prompt.RequiredItem.DisplayName
                    },
                    commands: new Dictionary<string, Action>
                    {
                        ["use_required_item"] = () => UseRequiredItem(prompt)
                    });
                return;
            }

            string text = resolution?.Text != null
                ? ExtractExamineText(resolution.Value.Text)
                : this.pendingExamineText;

            TypewriterRoutine(text).Forget();
        }

        // Registered as the "use_required_item" Yarn command for the duration of a
        // hotspot's prompt dialogue (see above) -- every prompt node's "Sí" branch calls
        // this same fixed command name.
        void UseRequiredItem(ExaminePrompt prompt)
        {
            if (this.inventoryService.TryRemoveItem(prompt.RequiredItem.ItemId))
                prompt.OnUsed?.Invoke();
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
