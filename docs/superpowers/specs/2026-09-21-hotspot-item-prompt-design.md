# Hotspot Item-Use Prompt — Design

## Problem

`ItemExamineHotspots` currently resolves every hotspot hit to static text,
typed out in `InspectPanel`. Some hotspots should instead let the player
*use* an inventory item on them — e.g. KI_05 (a suitcase)'s lock hotspot:
if the player has the right key, examining the lock should offer "Do you
want to use {item}? -Sí -No" instead of just describing the lock.

## Goal

Let a `Hotspot` optionally require an `ItemData`. When the player examines
that hotspot and the required item is in their inventory, a real Yarn
dialogue (question + Sí/No options) runs instead of the normal typed text.
Choosing "Sí" consumes the item and fires a per-hotspot `UnityEvent` hook
for any further gameplay effect. Hotspots without a required item are
completely unaffected.

## Non-goals (explicitly out of scope for this pass)

- **No post-use state tracking.** After the item is consumed, re-examining
  the same hotspot just re-resolves normally — since the item is gone,
  `HasItem` will be false and the hotspot falls back to its normal
  `dialogue` text. Whether that text still makes narrative sense after use
  is a content-authoring concern, not a code one. If a hotspot needs a
  distinct "already used" line later, that's a follow-up.
- **No new UI.** The Sí/No prompt runs through the project's existing
  general-purpose `DialogueRunner`/`IDialogueService` (the same one
  `pickup_prompt` and door dialogue already use) — no options UI is built
  for this feature.
- **No new Yarn tag.** Prompt nodes are authored and picked exactly like
  any other examine node, through the existing `examine`-tag-filtered
  picker (`ItemExamineHotspotsDrawer`).
- **No automatic re-typing after the dialogue closes.** Whether "Sí" or
  "No" was chosen, control just returns to `InspectPanel` as it was; the
  player presses Confirm again if they want to see updated text.

## Architecture

```
InspectPanel.OnConfirmPressed
        |
        v
  modelPreview.TryGetExamineDialogue()   -- PickupPreviewView
        |
        |  raycast -> hit collider (or none)
        v
  hotspots.Resolve(hitCollider, inventoryService)  -- ItemExamineHotspots
        |
        +-- no ItemExamineHotspots on model -> null
        |                                        -> InspectPanel falls back
        |                                           to pendingExamineText
        |                                           (unchanged)
        |
        +-- ExamineResolution.Text set      -> InspectPanel types it
        |    (no requiredItem, or requiredItem    (unchanged typewriter path)
        |     not in inventory, or promptDialogue
        |     invalid/unconfigured)
        |
        +-- ExamineResolution.Prompt set    -> InspectPanel calls
             (requiredItem present in           dialogueService.StartDialogue(
              inventory, promptDialogue valid)     prompt.Dialogue.nodeName,
                                                     commands: { "use_required_item":
                                                       () => UseRequiredItem(prompt) })
                                                   -- runs the project's normal
                                                      Yarn dialogue UI, pausing
                                                      InspectPanel meanwhile
```

`ExamineResolution` is resolved fresh on every Confirm press, same as
today — rotating between attempts, or losing the required item mid-session,
changes the outcome on the next press.

## Components

### `ItemExamineHotspots` changes

`Hotspot` gains three fields, all optional (default = today's behavior):

```csharp
[Serializable]
public struct Hotspot
{
    public Collider          collider;
    public DialogueReference dialogue;

    // Optional item-use prompt. If requiredItem is null, this hotspot
    // behaves exactly as before.
    public ItemData?          requiredItem;
    public DialogueReference  promptDialogue; // Yarn node with -> Sí / -> No
    public UnityEvent         onUsed;         // fires after the item is consumed
}
```

New result types (pure data, no Unity dependencies beyond what
`DialogueReference`/`ItemData` already pull in):

```csharp
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
```

`GetDialogue(Collider?)` stays exactly as-is (still covered by its 4
existing tests, unchanged contract, no `IsValid` check — that check has
always lived in the caller). Its inline match loop is extracted into a
private `FindHotspot` helper so `Resolve` can reuse it instead of
duplicating the loop:

```csharp
private Hotspot? FindHotspot(Collider? hitCollider)
{
    if (hitCollider == null) return null;
    foreach (var h in this.hotspots)
        if (h.collider == hitCollider) return h;
    return null;
}

public DialogueReference GetDialogue(Collider? hitCollider) =>
    FindHotspot(hitCollider)?.dialogue ?? this.defaultDialogue;
```

New method, the entry point `PickupPreviewView` calls instead of
`GetDialogue`. It owns the `IsValid` check that previously lived in
`PickupPreviewView.TryGetExamineDialogue()` (the I2 fix) — centralizing it
here means one place decides "is there anything usable to show", instead
of splitting that decision across two files:

```csharp
// Takes IInventoryService as a parameter, not injected -- this component
// lives on a prefab instantiated at runtime via Instantiate(), outside
// VContainer's build graph (see PickupPreviewView's own precedent).
//
// Returns null when nothing usable resolves at all (no matched hotspot AND
// no valid defaultDialogue, or a matched hotspot with an invalid dialogue
// and no valid defaultDialogue either) -- same "caller falls back to
// ItemData.ExamineDialogue" contract PickupPreviewView.TryGetExamineDialogue()
// already documents.
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
```

### `PickupPreviewView.TryGetExamineDialogue()` → returns `ExamineResolution?`

Same raycast/layer/`Physics.SyncTransforms()` logic as today; the method
now just forwards to `Resolve` instead of calling `GetDialogue` and
applying its own `IsValid` check (that check now lives in `Resolve`, see
above):

```csharp
public ExamineResolution? TryGetExamineDialogue(IInventoryService inventory)
{
    if (this.currentInstance == null) return null;

    var hotspots = this.currentInstance.GetComponentInChildren<ItemExamineHotspots>();
    if (hotspots == null) return null;

    // ... unchanged raycast block, produces hitCollider ...

    return hotspots.Resolve(hitCollider, inventory);
}
```

This is a breaking signature change to code committed earlier this branch.
`InspectPanel` is the only consumer today.

### `InspectPanel` changes

Two new injected dependencies (both already registered in
`NavigationScope`, no scope changes needed):

```csharp
[Inject] private IInventoryService inventoryService = null!;
[Inject] private IDialogueService  dialogueService   = null!;
```

`OnConfirmPressed` branches on the resolution:

```csharp
var resolution = this.modelPreview?.TryGetExamineDialogue(this.inventoryService);

if (resolution?.Prompt is { } prompt)
{
    this.dialogueService.StartDialogue(
        prompt.Dialogue.nodeName ?? string.Empty,
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
```

```csharp
private void UseRequiredItem(ExaminePrompt prompt)
{
    if (this.inventoryService.TryRemoveItem(prompt.RequiredItem.ItemId))
        prompt.OnUsed?.Invoke();
}
```

`Update()`'s rotation lock extends to also hold while a prompt dialogue is
running:

```csharp
if (!IsOpen || this.modelPreview == null || this.isTyping || this.dialogueService.IsRunning) return;
```

**Yarn authoring convention:** any hotspot's `promptDialogue` node must
call a command literally named `use_required_item` in its "Sí" branch —
that's the one fixed command name the whole system registers. Example for
KI_05's lock:

```
title: examine_KI_05_lock_prompt
tags: examine
---
¿Quieres usar {$item_name}?
-> Sí
    <<use_required_item>>
-> No
===
```

(Whether `{$item_name}` needs a variable set before this runs, vs. writing
the item name directly into the node, is a content decision at
implementation time — `StartDialogue` supports passing `variables` if
needed.)

### `IInventoryService` additions

Two new read/mutate methods, alongside the existing `TryUseKey`:

```csharp
/// <summary>Returns true if any slot holds an item with the given itemId.</summary>
bool HasItem(string itemId);

/// <summary>Finds and clears the first slot holding an item with the given
/// itemId. Returns false (no mutation) if not found.</summary>
bool TryRemoveItem(string itemId);
```

## Data flow summary

1. Player rotates the model onto a hotspot that has `requiredItem` set and
   presses Confirm.
2. `InspectPanel` asks `PickupPreviewView` to resolve, passing the injected
   `IInventoryService`.
3. `ItemExamineHotspots.Resolve` checks: is `requiredItem` in inventory,
   and is `promptDialogue` valid? If yes, returns an `ExaminePrompt`;
   otherwise returns the normal `Text`.
4. `InspectPanel` sees `Prompt` set and starts that Yarn node via
   `IDialogueService`, registering the fixed `use_required_item` command.
   The project's normal dialogue UI takes over; `InspectPanel`'s own
   rotation input is locked while `dialogueService.IsRunning`.
5. Player picks Sí or No in that dialogue.
   - **Sí**: the node's `<<use_required_item>>` line fires
     `UseRequiredItem`, which removes the item from inventory and invokes
     `prompt.OnUsed` (designer-configured — e.g. an interactable's own
     `UnityEvent` used to swap a container's contents, matching
     `DoorInteractable.onOpen`'s existing pattern).
   - **No**: nothing happens beyond closing the dialogue.
6. Dialogue completes, control returns to `InspectPanel` unchanged. Next
   Confirm press re-resolves from scratch (per the data flow above).

## Testing

`ItemExamineHotspots.Resolve(Collider?, IInventoryService)` is pure logic
(no raycast, no Unity physics) — testable in EditMode with a plain C# fake
implementing `IInventoryService`, following the project's established
fakes pattern. Covers: no `requiredItem` (unchanged behavior, falls to
`Text`), item present + valid prompt (returns `Prompt`), item present +
invalid/unconfigured `promptDialogue` (falls back to `Text`), item absent
(falls back to `Text`), and the existing invalid-dialogue-and-no-default
case (returns `null`) carried over from the current `TryGetExamineDialogue`
tests' coverage.

Everything downstream of the raycast (`PickupPreviewView`,
`InspectPanel`, the live `IDialogueService` run) has no automated coverage
for the same reason as the rest of this feature — verified manually in the
Editor, same approach as the original hotspots feature's Task 4.

## Content note

KI_05 (the suitcase) is the first real use case: its lock hotspot
(`examine_KI_05_lock`, already authored) gets a `requiredItem` pointing at
the small key (KI_10 or KI_12 — both are "a small key, just the right size
for a padlock or a small mechanism") and a new `promptDialogue` node
(`examine_KI_05_lock_prompt`, to be authored). What "Sí" actually *does*
beyond consuming the key (open the suitcase, reveal contents, etc.) is
deliberately left to `onUsed`/a follow-up task, per this design's
non-goals.
