#nullable enable

#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;
using UnityEngine.UI;
using VContainer.Unity;

namespace CrimsonDraft.Infrastructure.DebugMenu
{
    // Debug-only: F1 opens a pausable menu listing every registered debug entry (IDebugMenu).
    // Up/Down moves the highlight, Enter runs an action or flips a toggle, Esc/F1 closes it.
    // While open the game is paused (Time.timeScale = 0) and every enabled input map is switched
    // off, so Enter/arrows never reach the gameplay; both are restored exactly on close.
    // Built in code like MilestoneDebugOverlay, so no scene or prefab has to carry it.
    public sealed class DebugMenuController : ITickable, IDisposable
    {
        private const Key ToggleKey = Key.F1;

        private readonly IDebugMenu          menu;
        private readonly DebugMenuSelection  selection;
        private readonly InputActionAsset    inputActions;
        private readonly List<InputActionMap> pausedMaps = new List<InputActionMap>();

        private GameObject?      root;
        private TextMeshProUGUI? text;
        private float            previousTimeScale = 1f;

        [Preserve]
        public DebugMenuController(IDebugMenu menu, InputActionAsset inputActions)
        {
            this.menu         = menu;
            this.inputActions = inputActions;
            this.selection    = new DebugMenuSelection(menu);
        }

        public bool IsOpen => this.root != null && this.root.activeSelf;

        void ITickable.Tick()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard[ToggleKey].wasPressedThisFrame)
            {
                if (IsOpen) Close(); else Open();
                return;
            }

            if (!IsOpen) return;

            if (keyboard.escapeKey.wasPressedThisFrame) { Close(); return; }
            if (keyboard.upArrowKey.wasPressedThisFrame)   this.selection.Move(-1);
            if (keyboard.downArrowKey.wasPressedThisFrame) this.selection.Move(1);
            if (keyboard.leftArrowKey.wasPressedThisFrame)  this.selection.CycleCurrent(-1);
            if (keyboard.rightArrowKey.wasPressedThisFrame) this.selection.CycleCurrent(1);
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                this.selection.ActivateCurrent();

            Refresh(); // toggle states can change under the menu, and entries can come and go
        }

        public void Open()
        {
            if (this.root == null) Build();
            this.root!.SetActive(true);

            this.previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            this.pausedMaps.Clear();
            foreach (var map in this.inputActions.actionMaps)
            {
                if (!map.enabled) continue;
                this.pausedMaps.Add(map);
                map.Disable();
            }

            Refresh();
        }

        public void Close()
        {
            if (this.root != null) this.root.SetActive(false);

            Time.timeScale = this.previousTimeScale;
            foreach (var map in this.pausedMaps) map.Enable();
            this.pausedMaps.Clear();
        }

        private void Refresh()
        {
            if (this.text == null) return;
            this.text.text = Format(this.menu.Entries, this.selection.Index);
        }

        // Rich text: group headers, a ">" marker and a highlight on the selected row, [ON]/[OFF] for toggles.
        internal static string Format(IReadOnlyList<DebugMenuEntry> entries, int selected)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<color=#BF3030><b>DEBUG MENU</b></color>   <color=#8A8780>Up/Down - Left/Right - Enter - F1/Esc</color>");

            if (entries.Count == 0)
            {
                sb.AppendLine().Append("<color=#8A8780>(no entries)</color>");
                return sb.ToString();
            }

            string? group = null;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Group != group)
                {
                    group = entry.Group;
                    sb.AppendLine().AppendLine($"<color=#8A8780>{group.ToUpperInvariant()}</color>");
                }

                string state  = entry.IsToggle ? (entry.IsOn ? "  <color=#58C46A>[ON]</color>" : "  <color=#8A8780>[OFF]</color>") : "";
                string line   = entry.IsChoice
                    ? (entry.CurrentOption is { } option ? $"{entry.Label}: < {option} >" : $"{entry.Label}: (none)")
                    : $"{entry.Label}{state}";
                sb.AppendLine(i == selected ? $"<mark=#BF303066><color=#FFFFFF>> {line}</color></mark>" : $"  {line}");
            }

            return sb.ToString();
        }

        private void Build()
        {
            this.root = new GameObject("DebugMenu");
            this.root.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(this.root);

            var canvas = this.root.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9500;
            var scaler = this.root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            var panel = new GameObject("Panel");
            panel.transform.SetParent(this.root.transform, false);
            var background = panel.AddComponent<Image>();
            background.color         = new Color(0.04f, 0.04f, 0.05f, 0.9f);
            background.raycastTarget = false;
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(14f, -14f);
            var layout = panel.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 12, 12);
            layout.childControlWidth = layout.childControlHeight = true;
            var fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var accent = new GameObject("Accent");
            accent.transform.SetParent(panel.transform, false);
            var accentImage = accent.AddComponent<Image>();
            accentImage.color         = new Color(0.75f, 0.19f, 0.19f, 1f);
            accentImage.raycastTarget = false;
            var accentRect = accent.GetComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0f, 0f);
            accentRect.anchorMax = new Vector2(0f, 1f);
            accentRect.pivot     = new Vector2(0f, 0.5f);
            accentRect.sizeDelta = new Vector2(3f, 0f);
            accent.AddComponent<LayoutElement>().ignoreLayout = true;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(panel.transform, false);
            this.text = textGo.AddComponent<TextMeshProUGUI>();
            this.text.fontSize         = 18f;
            this.text.color            = new Color(0.91f, 0.89f, 0.84f, 1f);
            this.text.lineSpacing      = 12f;
            this.text.raycastTarget    = false;
            this.text.textWrappingMode = TextWrappingModes.NoWrap;
        }

        void IDisposable.Dispose()
        {
            if (IsOpen) Close();
            if (this.root == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(this.root);
            else UnityEngine.Object.DestroyImmediate(this.root);
        }
    }
}

#endif
