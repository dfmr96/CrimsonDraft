#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Yarn.Unity;
using CrimsonDraft.Infrastructure.UI;

namespace CrimsonDraft.Navigation.Dialogue
{
    /// <summary>
    /// While a dialogue runs, draws its canvas through the <see cref="UiOnlyEffectRig"/> so the text
    /// gets the UI volume (CRT / bloom) without touching the world. The inspect runner is not listed
    /// on purpose: its prompts live inside the inventory, which has its own look.
    /// </summary>
    public sealed class DialogueUiOnlyEffect : MonoBehaviour
    {
        [Serializable]
        private struct Route
        {
            public DialogueRunner runner;
            public Canvas         canvas;
        }

        [SerializeField] private Route[] routes = Array.Empty<Route>();

        private readonly List<(DialogueRunner runner, UnityAction start, UnityAction complete)> subscriptions = new();
        private readonly Dictionary<Canvas, Camera?> originalCameras = new();

        void OnEnable()
        {
            foreach (var route in this.routes)
            {
                if (route.runner == null || route.canvas == null) continue;

                var runner = route.runner;
                var canvas = route.canvas;
                UnityAction start    = () => OnStart(runner, canvas);
                UnityAction complete = () => OnComplete(runner, canvas);

                runner.onDialogueStart?.AddListener(start);
                runner.onDialogueComplete?.AddListener(complete);
                this.subscriptions.Add((runner, start, complete));
            }
        }

        void OnDisable()
        {
            foreach (var (runner, start, complete) in this.subscriptions)
            {
                if (runner == null) continue;
                runner.onDialogueStart?.RemoveListener(start);
                runner.onDialogueComplete?.RemoveListener(complete);
            }
            this.subscriptions.Clear();

            foreach (var (canvas, original) in this.originalCameras)
                if (canvas != null) canvas.worldCamera = original;
            this.originalCameras.Clear();

            // The rig only drops owners it is told about (or that were destroyed).
            foreach (var route in this.routes)
                if (route.runner != null) UiOnlyEffectRig.Current?.Release(route.runner);
        }

        private void OnStart(DialogueRunner owner, Canvas canvas)
        {
            var rig = UiOnlyEffectRig.Current;
            if (rig == null) return;

            if (!this.originalCameras.ContainsKey(canvas))
                this.originalCameras[canvas] = canvas.worldCamera;

            canvas.worldCamera = rig.Camera;
            rig.Acquire(owner);
        }

        private void OnComplete(DialogueRunner owner, Canvas canvas)
        {
            if (this.originalCameras.TryGetValue(canvas, out var original))
            {
                canvas.worldCamera = original;
                this.originalCameras.Remove(canvas);
            }
            UiOnlyEffectRig.Current?.Release(owner);
        }
    }
}
