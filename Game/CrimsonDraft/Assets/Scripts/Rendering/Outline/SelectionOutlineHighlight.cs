#nullable enable

using System;
using UnityEngine;

namespace CrimsonDraft.Rendering.Outline
{
    // Shared implementation of the screen-space edge-detect selection outline (see
    // OutlineRendererFeature): moves a target's own renderers onto the "Outline" layer while
    // selected, restoring each one's original layer on Clear(). Originally written inline in
    // GeneratorSwitchPanel; extracted so the main menu's knobs/buttons can use the exact same
    // technique instead of the older KnobOutline inverted-hull material.
    public sealed class SelectionOutlineHighlight
    {
        private Renderer[] renderers      = Array.Empty<Renderer>();
        private int[]      originalLayers = Array.Empty<int>();

        public void Show(Transform target)
        {
            Clear();

            // Looked up per-call rather than cached in a static field -- a static field
            // initializer runs during the owning MonoBehaviour's own field initialization,
            // and Unity forbids calling LayerMask.NameToLayer from there (throws even outside
            // Play mode, e.g. right after a domain reload re-deserializes the scene).
            int outlineLayer = LayerMask.NameToLayer("Outline");

            this.renderers      = target.GetComponentsInChildren<Renderer>();
            this.originalLayers = new int[this.renderers.Length];
            for (int i = 0; i < this.renderers.Length; i++)
            {
                this.originalLayers[i] = this.renderers[i].gameObject.layer;
                this.renderers[i].gameObject.layer = outlineLayer;
            }
        }

        public void Clear()
        {
            for (int i = 0; i < this.renderers.Length; i++)
                this.renderers[i].gameObject.layer = this.originalLayers[i];

            this.renderers      = Array.Empty<Renderer>();
            this.originalLayers = Array.Empty<int>();
        }
    }
}
