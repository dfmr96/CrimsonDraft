#nullable enable

#if UNITY_EDITOR

using System;
using TMPro;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UI;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.DebugMenu;

namespace CrimsonDraft.Navigation.Milestones
{
    // Debug-only: a debug menu toggle (group "Overlays") shows a box listing the titles of the milestones available right now
    // (see MilestoneAvailability), one per line. Built in code like ScreenFader, so no scene or
    // prefab has to carry it; refreshed whenever a milestone completes while it is shown.
    public sealed class MilestoneDebugOverlay : IInitializable, IDisposable
    {
        private readonly IDebugMenu        menu;
        private readonly MilestoneRegistry registry;
        private readonly MilestoneGraph[]  graphs;

        private GameObject?       root;
        private TextMeshProUGUI?  text;
        private IDisposable?      registration;

        [Preserve]
        public MilestoneDebugOverlay(IDebugMenu menu, MilestoneRegistry registry, MilestoneGraph[] graphs)
        {
            this.menu     = menu;
            this.registry = registry;
            this.graphs   = graphs;
            this.registry.Completed += OnCompleted;
        }

        void IInitializable.Initialize()
        {
            this.registration = this.menu.Register(DebugMenuEntry.Toggle("Overlays", "Available milestones",
                () => IsVisible, _ => Toggle()));
        }

        public bool   IsVisible => this.root != null && this.root.activeSelf;
        public string Text      => this.text != null ? this.text.text : "";

        public void Toggle()
        {
            if (this.root == null) Build();
            this.root!.SetActive(!this.root.activeSelf);
            if (this.root.activeSelf) Refresh();
        }

        private void OnCompleted(string _)
        {
            if (this.root != null && this.root.activeSelf) Refresh();
        }

        private void Refresh()
        {
            if (this.text != null)
                this.text.text = MilestoneAvailability.FormatList(MilestoneAvailability.AvailableTitles(this.graphs, this.registry));
        }

        private void Build()
        {
            this.root = new GameObject("MilestoneDebugOverlay");
            this.root.SetActive(false); // built hidden: the Toggle() that built it is what shows it
            var canvas = this.root.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9000;
            var scaler = this.root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            // Dark box at the top-left that grows with the list.
            var panel = new GameObject("Panel");
            panel.transform.SetParent(this.root.transform, false);
            var background = panel.AddComponent<Image>();
            background.color         = new Color(0.04f, 0.04f, 0.05f, 0.82f);
            background.raycastTarget = false;
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(14f, -14f);
            var layout = panel.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 10, 10);
            layout.childControlWidth = layout.childControlHeight = true;
            var fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Red accent edge on the left.
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
            this.text.fontSize      = 18f;
            this.text.color         = new Color(0.91f, 0.89f, 0.84f, 1f);
            this.text.lineSpacing   = 20f;
            this.text.raycastTarget = false;
            this.text.textWrappingMode = TextWrappingModes.NoWrap;
        }

        void IDisposable.Dispose()
        {
            this.registry.Completed -= OnCompleted;
            this.registration?.Dispose();
            if (this.root == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(this.root);
            else UnityEngine.Object.DestroyImmediate(this.root);
        }
    }
}

#endif
