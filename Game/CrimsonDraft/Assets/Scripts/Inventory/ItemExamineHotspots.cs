#nullable enable

using System;
using UnityEngine;
using UnityEngine.Events;
using Yarn.Unity;

namespace CrimsonDraft.Inventory
{
    // Placed on an ItemData.PreviewModel prefab to make specific colliders on the model
    // resolve to different examine dialogue when hit by PickupPreviewView's inspect raycast.
    // Items without this component keep using ItemData.ExamineDialogue unchanged.
    public sealed class ItemExamineHotspots : MonoBehaviour
    {
        [Serializable]
        public struct Hotspot
        {
            public Collider collider;
            public DialogueReference dialogue;

            // Optional item-use prompt. If requiredItem is null, this hotspot behaves
            // exactly as if these three fields didn't exist.
            public ItemData?         requiredItem;
            public DialogueReference promptDialogue;
            public UnityEvent?       onUsed;
        }

        [SerializeField] private Hotspot[] hotspots = Array.Empty<Hotspot>();
        [SerializeField] private DialogueReference defaultDialogue = new();

        private Hotspot? FindHotspot(Collider? hitCollider)
        {
            if (hitCollider == null) return null;
            foreach (var h in this.hotspots)
                if (h.collider == hitCollider) return h;
            return null;
        }

        public DialogueReference GetDialogue(Collider? hitCollider) =>
            FindHotspot(hitCollider)?.dialogue ?? this.defaultDialogue;

        // Takes IInventoryService as a parameter, not injected -- this component lives on
        // a prefab instantiated at runtime via Instantiate(), outside VContainer's build
        // graph (same reasoning as PickupPreviewView's own lack of injection).
        //
        // Returns null when nothing usable resolves at all (no matched hotspot and no
        // valid defaultDialogue, or a matched hotspot with an invalid dialogue and no
        // valid defaultDialogue either) -- callers fall back to ItemData.ExamineDialogue
        // in that case, same contract PickupPreviewView.TryGetExamineDialogue() already
        // documented before this method existed.
        public ExamineResolution? Resolve(Collider? hitCollider, IInventoryService inventory)
        {
            var hotspot = FindHotspot(hitCollider);

            if (hotspot is { } h
                && h.requiredItem != null
                && inventory.HasItem(h.requiredItem.ItemId)
                && h.promptDialogue.IsValid)
            {
                return ExamineResolution.ForPrompt(
                    new ExaminePrompt(h.promptDialogue, h.requiredItem, h.onUsed));
            }

            var candidate = hotspot?.dialogue;
            var text = (candidate != null && candidate.IsValid) ? candidate : this.defaultDialogue;
            return text.IsValid ? ExamineResolution.ForText(text) : null;
        }

        void OnDrawGizmosSelected() => DrawGizmos();

        // Exposed so other systems (PickupPreviewView's raycast debug gizmo) can draw
        // these same wireframes against a live runtime instance without needing to
        // separately select it in the hierarchy.
        public void DrawGizmos()
        {
            Gizmos.color = Color.yellow;
            foreach (var h in this.hotspots)
            {
                if (h.collider == null) continue;
                Gizmos.matrix = h.collider.transform.localToWorldMatrix;
                switch (h.collider)
                {
                    case BoxCollider box:
                        Gizmos.DrawWireCube(box.center, box.size);
                        break;
                    case SphereCollider sphere:
                        Gizmos.DrawWireSphere(sphere.center, sphere.radius);
                        break;
                    default:
                        Gizmos.DrawWireCube(Vector3.zero, h.collider.bounds.size);
                        break;
                }
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
    }

    public readonly struct ExaminePrompt
    {
        public readonly DialogueReference Dialogue;
        public readonly ItemData          RequiredItem;
        public readonly UnityEvent?       OnUsed;

        public ExaminePrompt(DialogueReference dialogue, ItemData requiredItem, UnityEvent? onUsed)
        {
            this.Dialogue     = dialogue;
            this.RequiredItem = requiredItem;
            this.OnUsed       = onUsed;
        }
    }

    public readonly struct ExamineResolution
    {
        public readonly DialogueReference? Text;
        public readonly ExaminePrompt?     Prompt;

        public static ExamineResolution ForText(DialogueReference text) => new(text, null);
        public static ExamineResolution ForPrompt(ExaminePrompt prompt) => new(null, prompt);

        private ExamineResolution(DialogueReference? text, ExaminePrompt? prompt)
        {
            this.Text   = text;
            this.Prompt = prompt;
        }
    }
}
