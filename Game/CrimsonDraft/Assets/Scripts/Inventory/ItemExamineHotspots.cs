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
            // exactly as if these fields didn't exist.
            public ItemData?         requiredItem;
            public DialogueReference promptDialogue;
            public UnityEvent?       onUsed;

            // Optional. A child of this model's root with a specific localRotation -- when
            // set, the preview rotates to match that rotation (see
            // PickupPreviewView.RotateMountPointTo) before onUsed fires, so a reveal
            // animation always plays from the same camera-friendly angle regardless of how
            // the player had the model rotated. Author it by rotating a child Transform in
            // Prefab Mode (where the root sits at identity) until it looks right.
            public Transform? activationTransform;

            // Optional reward granted once onUsed's reveal animation finishes. If reward is
            // null, activation stops after onUsed fires (unchanged behavior). Polymorphic --
            // right-click the field in the Inspector to pick a concrete kind (ItemHotspotReward,
            // NoteHotspotReward, ...); see HotspotReward for why it's split this way.
            [SerializeReference] public HotspotReward? reward;

            // The clip onUsed's animation plays -- its length is how long InspectPanel waits
            // before consuming the inspected item and granting reward, so the reward doesn't
            // appear mid-animation. Assign the same clip wired into onUsed's Animator (e.g.
            // the suitcase's "Open" state's motion).
            public AnimationClip? rewardAnimationClip;
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

            if (hotspot is { } h && (h.requiredItem != null || h.onUsed != null || h.reward != null))
            {
                // requiredItem is optional gating, not a prerequisite for activating at all --
                // a hotspot with no requiredItem activates unconditionally (e.g. a book that
                // just opens itself); one with requiredItem still needs it present plus a valid
                // Yes/No prompt, same as before.
                bool canActivate = h.requiredItem == null
                    || (inventory.HasItem(h.requiredItem.ItemId) && h.promptDialogue.IsValid);

                if (canActivate)
                {
                    return ExamineResolution.ForPrompt(
                        new ExaminePrompt(h.dialogue, h.promptDialogue, h.requiredItem, h.onUsed, h.activationTransform,
                            h.reward, h.rewardAnimationClip));
                }
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
        // The hotspot's ordinary examine text -- InspectPanel shows this first (same as any
        // plain hotspot) before advancing to Dialogue (the Yes/No prompt) on the next Confirm.
        public readonly DialogueReference FlavorDialogue;
        public readonly DialogueReference Dialogue;
        public readonly ItemData?         RequiredItem;
        public readonly UnityEvent?       OnUsed;
        public readonly Transform?        ActivationTransform;
        public readonly HotspotReward?    Reward;
        public readonly AnimationClip?    RewardAnimationClip;

        public ExaminePrompt(
            DialogueReference flavorDialogue, DialogueReference dialogue, ItemData? requiredItem, UnityEvent? onUsed,
            Transform? activationTransform, HotspotReward? reward, AnimationClip? rewardAnimationClip)
        {
            this.FlavorDialogue       = flavorDialogue;
            this.Dialogue             = dialogue;
            this.RequiredItem         = requiredItem;
            this.OnUsed               = onUsed;
            this.ActivationTransform  = activationTransform;
            this.Reward               = reward;
            this.RewardAnimationClip  = rewardAnimationClip;
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
