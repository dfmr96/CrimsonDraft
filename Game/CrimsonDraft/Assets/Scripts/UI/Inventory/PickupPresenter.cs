#nullable enable

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using VContainer.Unity;
using Yarn.Markup;
using Yarn.Unity;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Navigation;
using CrimsonDraft.Navigation.Interactables.UI;

namespace CrimsonDraft.UI
{
    public class PickupPresenter : DialoguePresenterBase
    {
        [Header("Panels")]
        [SerializeField] private GameObject root             = null!;

        [Header("Line")]
        [SerializeField] private TMP_Text      lineText      = null!;
        [SerializeField] private MarkupPalette? markupPalette;

        // Off by default -- PickupDialogueSystem's world pickup prompts ("Pick up X?") keep
        // showing their line instantly. InspectPromptDialogueSystem's instance opts in so its
        // hotspot prompts match the typewriter used everywhere else in InspectPanel.
        [SerializeField] private bool  useTypewriterForLine     = false;
        [SerializeField] private float typewriterCharsPerSecond = 40f;

        [Header("Options")]
        [SerializeField] private Transform        optionsContainer = null!;
        [SerializeField] private PickupOptionItem optionPrefab     = null!;

        [Header("Audio")]
        [SerializeField] private InventorySfxData? sfxData;

        private readonly List<PickupOptionItem> pool = new();

        private IInputService? inputService;

        private int  selectedIndex;
        private int  optionCount;
        private bool isSelectingOption;
        private YarnTaskCompletionSource<DialogueOption?>? optionTcs;
        private float lastNavigateTime;
        private const float NavigateCooldown = 0.2f;

        private bool isTypingLine;
        private bool skipLineRequested;

        // ── Lifecycle ────────────────────────────────────────────────────────

        void Start()
        {
            root.SetActive(false);

            // Resolve IInputService from the scene's VContainer scope.
            // CrimsonDraft.UI.Prototype references CrimsonDraft.Navigation,
            // so NavigationScope is visible here without adding a circular dep.
            var scope = FindAnyObjectByType<NavigationScope>();
            if (scope != null)
            {
                try { inputService = scope.Container.Resolve<IInputService>(); }
                catch (VContainerException) { /* scene without full DI setup */ }
            }
        }

        // ── DialoguePresenterBase ────────────────────────────────────────────

        public override YarnTask OnDialogueStartedAsync()
        {
            inputService?.SwitchToPickupPrompt();
            root.SetActive(true);
            return YarnTask.CompletedTask;
        }

        public override async YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
        {
            string text = ApplyMarkupColor(line.Text, "item", "orange");

            if (!useTypewriterForLine)
            {
                lineText.text = text;
                return;
            }

            skipLineRequested = false;
            isTypingLine       = true;
            lineText.text      = string.Empty;

            // Same reasoning as PickupPresenter's option navigation below: PickupConfirm is
            // only wired for the duration it's needed, here to let the player skip the reveal
            // early instead of waiting it out.
            Action<InputAction.CallbackContext>? onSkip = null;
            if (inputService != null)
            {
                onSkip = _ => skipLineRequested = true;
                inputService.PickupConfirm.performed += onSkip;
            }

            float charInterval = 1f / Mathf.Max(1f, typewriterCharsPerSecond);
            float timer         = 0f;
            int   revealed      = 0;

            while (revealed < text.Length)
            {
                if (skipLineRequested) { revealed = text.Length; break; }

                timer += Time.unscaledDeltaTime;
                while (timer >= charInterval && revealed < text.Length)
                {
                    revealed++;
                    timer -= charInterval;
                }

                lineText.text = text.Substring(0, revealed);
                await YarnTask.Yield();
            }

            lineText.text = text;
            isTypingLine  = false;

            if (onSkip != null) inputService!.PickupConfirm.performed -= onSkip;
        }

        private static string ApplyMarkupColor(MarkupParseResult markup, string markerName, string color)
        {
            var text = markup.Text;
            for (int i = markup.Attributes.Count - 1; i >= 0; i--)
            {
                var attr = markup.Attributes[i];
                if (attr.Name != markerName) continue;
                text = text.Substring(0, attr.Position)
                     + $"<color={color}>"
                     + text.Substring(attr.Position, attr.Length)
                     + "</color>"
                     + text.Substring(attr.Position + attr.Length);
            }
            return text;
        }

        public override async YarnTask<DialogueOption?> RunOptionsAsync(
            DialogueOption[] options, LineCancellationToken token)
        {
            EnsurePool(options.Length);

            optionTcs     = new YarnTaskCompletionSource<DialogueOption?>();
            selectedIndex = 0;
            optionCount   = options.Length;

            for (int i = 0; i < options.Length; i++)
            {
                var item        = pool[i];
                var capturedIdx = i;
                item.gameObject.SetActive(true);
                item.Setup(options[i]);
                item.Hovered = _ => { selectedIndex = capturedIdx; HighlightOnly(pool[capturedIdx]); };
                item.Clicked = clicked => { PlayConfirmSfx(clicked.Option); optionTcs.TrySetResult(clicked.Option); };
            }

            for (int i = options.Length; i < pool.Count; i++)
                pool[i].gameObject.SetActive(false);

            pool[0].SetHighlight(true);
            isSelectingOption = true;

            // Wire PickupPrompt action callbacks when IInputService is available
            Action<InputAction.CallbackContext>? onNavigate = null;
            Action<InputAction.CallbackContext>? onConfirm  = null;

            if (inputService != null)
            {
                onNavigate = ctx =>
                {
                    if (Time.unscaledTime - lastNavigateTime < NavigateCooldown) return;
                    lastNavigateTime = Time.unscaledTime;
                    var v = ctx.ReadValue<Vector2>();
                    if      (v.x >  0.5f) ShiftSelection(1);
                    else if (v.x < -0.5f) ShiftSelection(-1);
                };
                onConfirm = _ =>
                {
                    PlayConfirmSfx(pool[selectedIndex].Option);
                    optionTcs?.TrySetResult(pool[selectedIndex].Option);
                };

                inputService.PickupNavigate.performed += onNavigate;
                inputService.PickupConfirm.performed  += onConfirm;
            }

            var result = await optionTcs.Task;

            isSelectingOption = false;

            if (onNavigate != null) inputService!.PickupNavigate.performed -= onNavigate;
            if (onConfirm  != null) inputService!.PickupConfirm.performed  -= onConfirm;

            foreach (var item in pool)
            {
                item.ClearListeners();
                item.gameObject.SetActive(false);
            }

            return result;
        }

        public override YarnTask OnDialogueCompleteAsync()
        {
            isSelectingOption = false;
            foreach (var item in pool)
                if (item != null) item.gameObject.SetActive(false);
            if (root != null) root.SetActive(false);
            inputService?.SwitchToGameplay();
            return YarnTask.CompletedTask;
        }

        // ── Input fallback (no IInputService — polling Keyboard/Gamepad) ─────

        void Update()
        {
            if (inputService != null) return;

            var kb = Keyboard.current;
            var gp = Gamepad.current;
            bool confirm = kb?.cKey.wasPressedThisFrame == true || gp?.buttonSouth.wasPressedThisFrame == true;

            if (isTypingLine)
            {
                if (confirm) skipLineRequested = true;
                return;
            }

            if (!isSelectingOption || optionTcs == null) return;

            bool right = kb?.rightArrowKey.wasPressedThisFrame == true || gp?.dpad.right.wasPressedThisFrame == true;
            bool left  = kb?.leftArrowKey.wasPressedThisFrame  == true || gp?.dpad.left.wasPressedThisFrame  == true;

            if (right) ShiftSelection(1);
            if (left)  ShiftSelection(-1);
            if (confirm)
            {
                PlayConfirmSfx(pool[selectedIndex].Option);
                optionTcs.TrySetResult(pool[selectedIndex].Option);
            }
        }

        // ── Internal ─────────────────────────────────────────────────────────

        private void PlayConfirmSfx(DialogueOption? option)
        {
            string text = option?.Line.Text.Text ?? string.Empty;
            if (string.Equals(text, "No", StringComparison.OrdinalIgnoreCase))
                sfxData?.PlayCancel(gameObject);
            else
                sfxData?.PlayDecide(gameObject);
        }

        private void ShiftSelection(int dir)
        {
            if (optionCount == 0) return;
            selectedIndex = (selectedIndex + dir + optionCount) % optionCount;
            for (int i = 0; i < optionCount; i++)
                pool[i].SetHighlight(i == selectedIndex);
            sfxData?.PlayCursor(gameObject);
        }

        private void EnsurePool(int count)
        {
            while (pool.Count < count)
                pool.Add(Instantiate(optionPrefab, optionsContainer));
        }

        private void HighlightOnly(PickupOptionItem target)
        {
            foreach (var item in pool)
                item.SetHighlight(item == target);
        }
    }
}
