# Item Examine Hotspots — Design

## Problem

`InspectPanel` currently shows one static examine text per item, taken from
`ItemData.ExamineDialogue` and revealed via typewriter on Confirm. Some items
should instead show *different* examine text depending on which part of the
3D model the player is currently looking at while rotating it in the inspect
viewer — e.g. a key with an embedded sheep on its bow: aiming at the bow and
pressing Confirm should show a specific line about the sheep, aiming anywhere
else should show a generic "old key" line.

## Goal

Let an item's `PreviewModel` prefab define one or more clickable "hotspots"
(colliders) that map to specific Yarn dialogue nodes, plus a default node for
when the player isn't aiming at any hotspot. `InspectPanel` resolves which
text to show by raycasting from the inspect camera at the moment the player
presses Confirm.

## Non-goals (explicitly out of scope for this pass)

- **No face/side detection.** A hotspot fires as soon as its collider is hit,
  regardless of which side of the collider the ray entered from. An
  item where something is only "visible" from one angle (e.g. text on one
  side of a sheet of paper) is handled by placement/composition (a thin
  collider covering only the relevant geometry) — not by any facing check in
  code. This was deliberately dropped after discussion; if it's needed later
  it can be reintroduced as an opt-in field on `Hotspot` without changing the
  rest of the design.
- **No MeshCollider-based backface culling.** Considered and rejected —
  hotspots use primitive colliders (Box/Sphere/Capsule) only.
- **No dedicated editor tool.** Hotspots are placed by hand in Scene view
  like any other collider. A `OnDrawGizmosSelected` visualization is the only
  authoring aid included.
- **No aim reticle/crosshair UI.** Out of scope for this pass.
- **Selecting individual faces of a single collider** isn't supported by any
  Unity API. The equivalent is achieved by composition: one thin collider
  per face that should be hittable, each its own `Hotspot` entry.

## Architecture

```
InspectPanel.OnConfirmPressed
        │
        │  (starting a fresh typewriter run)
        ▼
  modelPreview.TryGetExamineDialogue()  ── PickupPreviewView
        │
        ├─ no ItemExamineHotspots on currentInstance → null
        │                                                  │
        │                                                  ▼
        │                                     InspectPanel falls back to
        │                                     pendingExamineText (existing
        │                                     ItemData.ExamineDialogue path,
        │                                     unchanged)
        │
        └─ has ItemExamineHotspots →
              Physics.Raycast(camera.position, camera.forward,
                               layerMask: ItemPreview, QueryTriggerInteraction.Collide)
              → hotspots.GetDialogue(hit.collider or null)
                    → matches a registered Hotspot.collider → that dialogue
                    → no match / no hit                    → defaultDialogue
```

Resolution happens **every time a new typewriter run starts** (i.e. on the
Confirm press that starts it), not once when the panel opens — so rotating
the model between examine attempts changes the result on the next press.
Items without hotspots keep the existing static, cached-at-`Open()` behavior.

## Components

### `ItemExamineHotspots` (new)

`Assets/Scripts/Inventory/ItemExamineHotspots.cs` — placed on the root (or
any child) of an `ItemData.PreviewModel` prefab that wants hotspot-driven
examine text. Items without this component are unaffected.

```csharp
#nullable enable
using System;
using UnityEngine;
using Yarn.Unity;

namespace CrimsonDraft.Inventory
{
    public sealed class ItemExamineHotspots : MonoBehaviour
    {
        [Serializable]
        public struct Hotspot
        {
            public Collider collider;
            public DialogueReference dialogue;
        }

        [SerializeField] private Hotspot[] hotspots = Array.Empty<Hotspot>();
        [SerializeField] private DialogueReference defaultDialogue = new();

        public DialogueReference GetDialogue(Collider? hitCollider)
        {
            if (hitCollider != null)
            {
                foreach (var h in this.hotspots)
                    if (h.collider == hitCollider) return h.dialogue;
            }
            return this.defaultDialogue;
        }

        void OnDrawGizmosSelected()
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
                    // CapsuleCollider: approximate with a wire cube of its bounds --
                    // good enough for an editor-only placement aid.
                    default:
                        Gizmos.DrawWireCube(Vector3.zero, h.collider.bounds.size);
                        break;
                }
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
```

Authoring a hotspot: add a primitive `Collider` (usually `BoxCollider`) as a
child of the model, size/position it over the relevant geometry, set
`isTrigger = true`, and add an entry to `hotspots` on
`ItemExamineHotspots` pointing at it with a Yarn `DialogueReference`. Leave
`defaultDialogue` pointing at the "nothing special" line.

Multiple hotspots per item are supported (list, not a single slot) — e.g. a
weapon could have separate hotspots for its barrel and an engraving.

To make only specific faces of a shape hittable (rather than the whole
volume), use several thin colliders, one per face, each its own `Hotspot`
entry — not one collider covering the whole shape.

### `PickupPreviewView` changes

New method, uses the existing `previewCamera` and `previewLayer` (see the
rotation work earlier this branch — `previewCamera` is `mountPoint.parent`'s
`Camera`, `previewLayer` is resolved from `previewLayerName` and already
applied recursively to every instantiated preview model):

```csharp
public DialogueReference? TryGetExamineDialogue()
{
    if (this.currentInstance == null) return null;

    var hotspots = this.currentInstance.GetComponentInChildren<ItemExamineHotspots>();
    if (hotspots == null) return null; // no hotspot system on this item

    Collider? hitCollider = null;
    if (this.previewCamera != null)
    {
        int mask = this.previewLayer >= 0 ? 1 << this.previewLayer : ~0;
        if (Physics.Raycast(this.previewCamera.transform.position, this.previewCamera.transform.forward,
                out var hit, Mathf.Infinity, mask, QueryTriggerInteraction.Collide))
        {
            hitCollider = hit.collider;
        }
    }

    return hotspots.GetDialogue(hitCollider);
}
```

Returns `null` (not a "miss" `DialogueReference`) when the item has no
`ItemExamineHotspots` at all — that's the caller's signal to fall back to the
item's normal `ExamineDialogue`, not to treat it as a hotspot miss.

### `InspectPanel` changes

`OnConfirmPressed` currently always starts the typewriter with
`this.pendingExamineText` (computed once in `OpenInternal`). It now asks the
preview for a hotspot-resolved dialogue first:

```csharp
void OnConfirmPressed(InputAction.CallbackContext _)
{
    if (!IsOpen) return;
    if (Time.frameCount == this.openedFrame) return;
    if (this.isTyping) { this.skipRequested = true; return; }

    var hotspotDialogue = this.modelPreview?.TryGetExamineDialogue();
    string text = hotspotDialogue != null
        ? ExtractExamineText(hotspotDialogue)
        : this.pendingExamineText;

    TypewriterRoutine(text).Forget();
}
```

`ExtractExamineText` (already implemented) is reused unchanged — it doesn't
care whether the `DialogueReference` came from `ItemData.ExamineDialogue` or
a hotspot.

No change to `OpenInternal`'s existing `pendingExamineText` computation —
still needed as the fallback for non-hotspot items and is cheap to compute
even when unused.

## Data flow summary

1. Player opens `InspectPanel` on an item with a hotspot-bearing
   `PreviewModel`. Model spawns under `mountPoint` as today; nothing hotspot
   related happens yet.
2. Player rotates the model (existing `SetRotationInput` feature).
3. Player presses Confirm. `InspectPanel` asks `PickupPreviewView` to raycast
   right now, gets back a `DialogueReference` (hotspot-specific or default),
   extracts its text, and types it out.
4. Player rotates further and presses Confirm again (once the previous
   typewriter run finished, per the existing replay behavior) — the raycast
   runs again and may resolve to different text.

## Testing

Same situation as the rotation feature earlier on this branch: this depends
on live Physics raycasts against real 3D geometry and authored Yarn content,
none of which fits the project's EditMode plain-C#-fakes pattern (no
Rigidbody/PhysX simulation available there, no real prefab hierarchy).
Verification is manual in the Editor: rotate the model, aim at a hotspot,
Confirm, check the hotspot's text appears; aim elsewhere, Confirm, check the
default text appears.

## Content note

No concrete item currently has a `PreviewModel` with hotspot geometry — the
"sheep key" used to describe this feature doesn't exist as an asset yet
(checked: no matching `.asset`/model in the project). Wiring up a real
example is a follow-up content task once a model with distinguishable
geometry (e.g. a raised bow shape) exists to place a collider on.
