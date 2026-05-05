# Yarn Spinner Dialogue System — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Install Yarn Spinner Unity and replace the custom `PoiController`/`PoiDialogView` dialogue system with an `IDialogueService` that wraps Yarn's `DialogueRunner`, with all player-facing text living in `.yarn` files.

**Architecture:** A new `DialogueService` (registered in `NavigationScope`) wraps `DialogueRunner`, handles time scale pausing and input map switching, populates `InMemoryVariableStorage` before each dialogue, and registers one-time command handlers per session. Every interactable calls `context.DialogueService.StartDialogue(nodeName, variables, onComplete, commands)` — no strings in C# code.

**Tech Stack:** Yarn Spinner Unity (UPM git), VContainer, Unity Input System, Unity TMP.

**Spec:** `docs/superpowers/specs/2026-04-25-yarn-spinner-dialogue-design.md`
**GDD:** `Sistema de Dialogos.md`
**Branch:** `feature/yarn-spinner`

---

## File Map

### Created
- `Game/CrimsonDraft/Assets/Scripts/Navigation/Dialogue/IDialogueService.cs`
- `Game/CrimsonDraft/Assets/Scripts/Navigation/Dialogue/DialogueService.cs`
- `Game/CrimsonDraft/Assets/Tests/EditMode/DoorInteractableTests.cs`
- `Game/CrimsonDraft/Assets/Dialogues/poi/poi_test.yarn`
- `Game/CrimsonDraft/Assets/Dialogues/doors/door_test.yarn`
- `Game/CrimsonDraft/Assets/Dialogues/sockets/socket_test.yarn`
- `Game/CrimsonDraft/Assets/Dialogues/Navigation.yarnproject` *(Unity editor asset — created manually)*

### Modified
- `Game/CrimsonDraft/Packages/manifest.json` — add YarnSpinner-Unity dependency
- `Game/CrimsonDraft/Assets/Scripts/Navigation/CrimsonDraft.Navigation.asmdef` — add YarnSpinner.Unity reference
- `Game/CrimsonDraft/Assets/Tests/EditMode/CrimsonDraft.Tests.EditMode.asmdef` — add YarnSpinner.Unity reference
- `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/Data/PoiData.cs` — replace `lines[]` with `yarnNodeName`
- `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/Data/DoorData.cs` — add `yarnNodeName`
- `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/InteractionContext.cs` — `PoiController` → `IDialogueService`
- `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/PlayerInteractionCaster.cs` — inject `IDialogueService`, remove `PoiController`
- `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/PoiInteractable.cs` — use `DialogueService`
- `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/DoorInteractable.cs` — full refactor per spec
- `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/ItemSocketInteractable.cs` — add `yarnNodeName`, update `TryInsert` + `Interact`
- `Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs` — swap registrations
- `Game/CrimsonDraft/Assets/Tests/EditMode/ItemSocketInteractableTests.cs` — update `TryInsert` call sites

### Deleted
- `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/UI/PoiController.cs`
- `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/UI/PoiDialogView.cs`

---

## Task 1: Install YarnSpinner-Unity package

**Files:**
- Modify: `Game/CrimsonDraft/Packages/manifest.json`

- [ ] **Add YarnSpinner-Unity to manifest.json**

Open `Game/CrimsonDraft/Packages/manifest.json`. Add this entry to the `"dependencies"` object (after `"com.coplaydev.unity-mcp"` line):

```json
"dev.yarnspinner.unity": "https://github.com/YarnSpinnerTool/YarnSpinner-Unity.git",
```

- [ ] **Let Unity resolve the package**

Save the file and switch to Unity. Wait for the progress bar to finish importing. Unity will download and compile Yarn Spinner.

- [ ] **Verify no compilation errors**

In Unity: Window → Console. Confirm zero errors. If there are errors, check the Unity version compatibility note in the Yarn Spinner README.

- [ ] **Find the assembly name**

In Unity: Window → Package Manager → find "Yarn Spinner for Unity" → click it. Note the package version. Then search the project for `YarnSpinner*.asmdef` in the Packages folder and note the exact `"name"` value — it is needed in Task 2.

The assembly name is likely `YarnSpinner.Unity`. Confirm and use that exact name in Tasks 2 and 3.

- [ ] **Commit**

```bash
git add Game/CrimsonDraft/Packages/manifest.json Game/CrimsonDraft/Packages/packages-lock.json
git commit -m "feat(yarn-spinner): install YarnSpinner-Unity package via UPM"
```

---

## Task 2: Update asmdefs to reference YarnSpinner

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/CrimsonDraft.Navigation.asmdef`
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/CrimsonDraft.Tests.EditMode.asmdef`

- [ ] **Add YarnSpinner.Unity to Navigation asmdef**

In `CrimsonDraft.Navigation.asmdef`, add `"YarnSpinner.Unity"` to the `references` array:

```json
{
    "name": "CrimsonDraft.Navigation",
    "rootNamespace": "CrimsonDraft.Navigation",
    "references": [
        "CrimsonDraft.Infrastructure",
        "CrimsonDraft.Inventory",
        "CrimsonDraft.Operators",
        "VContainer",
        "VContainer.Unity",
        "UniTask",
        "Unity.InputSystem",
        "Unity.TextMeshPro",
        "YarnSpinner.Unity"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Add YarnSpinner.Unity to test asmdef**

In `CrimsonDraft.Tests.EditMode.asmdef`, add `"YarnSpinner.Unity"` to `references`:

```json
{
    "name": "CrimsonDraft.Tests.EditMode",
    "rootNamespace": "CrimsonDraft.Tests",
    "references": [
        "CrimsonDraft.UI",
        "CrimsonDraft.Infrastructure",
        "CrimsonDraft.Combat",
        "CrimsonDraft.Operators",
        "CrimsonDraft.Inventory",
        "CrimsonDraft.Navigation",
        "VContainer",
        "VContainer.Unity",
        "MessagePipe",
        "Unity.TextMeshPro",
        "YarnSpinner.Unity",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": ["nunit.framework.dll"],
    "autoReferenced": false,
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Verify compilation in Unity**

Switch to Unity. Console must show zero errors.

- [ ] **Commit**

```bash
git add Game/CrimsonDraft/Assets/Scripts/Navigation/CrimsonDraft.Navigation.asmdef
git add Game/CrimsonDraft/Assets/Tests/EditMode/CrimsonDraft.Tests.EditMode.asmdef
git commit -m "feat(yarn-spinner): add YarnSpinner.Unity asmdef references"
```

---

## Task 3: IDialogueService interface

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Dialogue/IDialogueService.cs`

- [ ] **Create the file**

```csharp
#nullable enable

using System;
using System.Collections.Generic;

namespace CrimsonDraft.Navigation.Dialogue
{
    public interface IDialogueService
    {
        bool IsRunning { get; }

        void StartDialogue(
            string                                  nodeName,
            IReadOnlyDictionary<string, object>?   variables  = null,
            Action?                                 onComplete = null,
            IReadOnlyDictionary<string, Action>?   commands   = null);
    }
}
```

- [ ] **Verify compilation in Unity** — zero errors.

- [ ] **Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Dialogue/IDialogueService.cs"
git commit -m "feat(yarn-spinner): add IDialogueService interface"
```

---

## Task 4: DialogueService implementation

**Files:**
- Create: `Game/CrimsonDraft/Assets/Scripts/Navigation/Dialogue/DialogueService.cs`

- [ ] **Create the file**

```csharp
#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;
using VContainer.Unity;
using Yarn.Unity;
using CrimsonDraft.Infrastructure.Input;

namespace CrimsonDraft.Navigation.Dialogue
{
    public sealed class DialogueService : IDialogueService, IInitializable
    {
        private readonly DialogueRunner          runner;
        private readonly InMemoryVariableStorage variableStorage;
        private readonly IInputService           inputService;

        private Action?      pendingOnComplete;
        private List<string> sessionCommandNames = new();

        [Preserve]
        public DialogueService(
            DialogueRunner          runner,
            InMemoryVariableStorage variableStorage,
            IInputService           inputService)
        {
            this.runner          = runner;
            this.variableStorage = variableStorage;
            this.inputService    = inputService;
        }

        public bool IsRunning => this.runner.IsDialogueRunning;

        void IInitializable.Initialize()
        {
            this.runner.onDialogueComplete.AddListener(OnDialogueComplete);
        }

        public void StartDialogue(
            string                                  nodeName,
            IReadOnlyDictionary<string, object>?   variables  = null,
            Action?                                 onComplete = null,
            IReadOnlyDictionary<string, Action>?   commands   = null)
        {
            this.pendingOnComplete = onComplete;

            this.variableStorage.Clear();
            if (variables != null)
            {
                foreach (var (key, value) in variables)
                {
                    switch (value)
                    {
                        case bool b:   this.variableStorage.SetValue(key, b); break;
                        case string s: this.variableStorage.SetValue(key, s); break;
                        case float f:  this.variableStorage.SetValue(key, f); break;
                        case int i:    this.variableStorage.SetValue(key, (float)i); break;
                    }
                }
            }

            this.sessionCommandNames.Clear();
            if (commands != null)
            {
                foreach (var (name, action) in commands)
                {
                    this.runner.AddCommandHandler(name, action);
                    this.sessionCommandNames.Add(name);
                }
            }

            Time.timeScale = 0f;
            this.inputService.SwitchToUI();
            this.runner.StartDialogue(nodeName);
        }

        private void OnDialogueComplete()
        {
            Time.timeScale = 1f;
            this.inputService.SwitchToGameplay();

            foreach (var name in this.sessionCommandNames)
                this.runner.RemoveCommandHandler(name);
            this.sessionCommandNames.Clear();

            var callback           = this.pendingOnComplete;
            this.pendingOnComplete = null;
            callback?.Invoke();
        }
    }
}
```

> **Note:** If `InMemoryVariableStorage.Clear()` does not exist in the installed version, check the Yarn Spinner source for the correct reset method (may be `ResetToDefaults()` or clearing via `SetValue` calls). Similarly verify `RemoveCommandHandler` — it exists in Yarn Spinner 2.x; in 3.x it may be on the `CommandDispatcher`.

- [ ] **Verify compilation in Unity** — zero errors.

- [ ] **Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Dialogue/DialogueService.cs"
git commit -m "feat(yarn-spinner): implement DialogueService"
```

---

## Task 5: Data layer — PoiData + DoorData yarnNodeName

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/Data/PoiData.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/Data/DoorData.cs`

> **Warning:** Removing `lines[]` from `PoiData` will erase the serialized string data on existing `PoiData` assets in the scene. This is expected — those strings will be rewritten as `.yarn` nodes in Task 9.

- [ ] **Replace PoiData.lines with yarnNodeName**

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    [CreateAssetMenu(menuName = "CrimsonDraft/Interactables/PoiData")]
    public sealed class PoiData : ScriptableObject
    {
        [SerializeField] private string yarnNodeName = "";

        public string YarnNodeName => this.yarnNodeName;
    }
}
```

- [ ] **Add yarnNodeName to DoorData**

```csharp
#nullable enable

using UnityEngine;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Interactables
{
    [CreateAssetMenu(menuName = "CrimsonDraft/Interactables/DoorData")]
    public sealed class DoorData : ScriptableObject
    {
        [SerializeField] private bool         locked       = false;
        [SerializeField] private KeyItemData? keyItem      = null;
        [SerializeField] private string       yarnNodeName = "";

        public bool         Locked       => this.locked;
        public KeyItemData? KeyItem      => this.keyItem;
        public string       YarnNodeName => this.yarnNodeName;
    }
}
```

- [ ] **Verify compilation in Unity** — zero errors.

- [ ] **Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/Data/PoiData.cs"
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/Data/DoorData.cs"
git commit -m "feat(yarn-spinner): add yarnNodeName to PoiData and DoorData"
```

---

## Task 6: Full interactable migration

This task replaces `PoiController` with `IDialogueService` across all call sites atomically. All files in this task must be saved before switching to Unity.

**Files:**
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/InteractionContext.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/PlayerInteractionCaster.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/PoiInteractable.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/DoorInteractable.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/ItemSocketInteractable.cs`
- Modify: `Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs`
- Modify: `Game/CrimsonDraft/Assets/Tests/EditMode/ItemSocketInteractableTests.cs`
- Delete: `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/UI/PoiController.cs`
- Delete: `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/UI/PoiDialogView.cs`

- [ ] **InteractionContext — replace PoiController with IDialogueService**

```csharp
#nullable enable

using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Dialogue;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class InteractionContext
    {
        public readonly IInventoryService   InventoryService;
        public readonly IInputService       InputService;
        public readonly IDialogueService    DialogueService;
        public readonly DocumentController  DocumentController;
        public readonly ContainerController ContainerController;

        public InteractionContext(
            IInventoryService   inventoryService,
            IInputService       inputService,
            IDialogueService    dialogueService,
            DocumentController  documentController,
            ContainerController containerController)
        {
            InventoryService    = inventoryService;
            InputService        = inputService;
            DialogueService     = dialogueService;
            DocumentController  = documentController;
            ContainerController = containerController;
        }
    }
}
```

- [ ] **PlayerInteractionCaster — inject IDialogueService, remove PoiController**

```csharp
#nullable enable

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;
using VContainer;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Dialogue;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class PlayerInteractionCaster : MonoBehaviour, IInteractionCaster
    {
        [SerializeField] private float     rayDistance = 2f;
        [SerializeField] private LayerMask interactableLayer;

        private IInputService       inputService        = null!;
        private IInventoryService   inventoryService    = null!;
        private IDialogueService    dialogueService     = null!;
        private DocumentController  documentController  = null!;
        private ContainerController containerController = null!;

        [Inject]
        public void Construct(
            IInputService       inputService,
            IInventoryService   inventoryService,
            IDialogueService    dialogueService,
            DocumentController  documentController,
            ContainerController containerController)
        {
            this.inputService        = inputService;
            this.inventoryService    = inventoryService;
            this.dialogueService     = dialogueService;
            this.documentController  = documentController;
            this.containerController = containerController;
            this.inputService.Interact.performed += OnInteract;
        }

        private void OnDestroy()
        {
            if (this.inputService != null)
                this.inputService.Interact.performed -= OnInteract;
        }

        private void OnInteract(InputAction.CallbackContext _)
        {
            if (!Physics.Raycast(transform.position, transform.forward, out var hit, this.rayDistance, this.interactableLayer))
                return;

            if (!hit.collider.TryGetComponent<IInteractable>(out var interactable))
                return;

            var context = new InteractionContext(
                this.inventoryService,
                this.inputService,
                this.dialogueService,
                this.documentController,
                this.containerController);
            interactable.Interact(context);
        }

        public bool CanUseItem(ItemData item)
        {
            if (!Physics.Raycast(transform.position, transform.forward, out var hit, this.rayDistance, this.interactableLayer))
                return false;
            if (!hit.collider.TryGetComponent<ItemSocketInteractable>(out var socket))
                return false;
            return socket.CanInsert(item);
        }

        public bool TryUseItem(ItemData item)
        {
            if (!Physics.Raycast(transform.position, transform.forward, out var hit, this.rayDistance, this.interactableLayer))
                return false;

            if (!hit.collider.TryGetComponent<ItemSocketInteractable>(out var socket))
                return false;

            return socket.TryInsert(item, this.dialogueService);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            var origin = transform.position;
            var tip    = origin + transform.forward * this.rayDistance;

            bool hit = Physics.Raycast(origin, transform.forward, out var hitInfo, this.rayDistance, this.interactableLayer);

            Gizmos.color = hit ? Color.green : Color.cyan;
            Gizmos.DrawRay(origin, transform.forward * this.rayDistance);
            Gizmos.DrawWireSphere(tip, 0.08f);

            if (hit)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(hitInfo.point, 0.12f);

                UnityEditor.Handles.color = Color.green;
                UnityEditor.Handles.Label(hitInfo.point + Vector3.up * 0.3f, hitInfo.collider.name);
            }
        }
#endif
    }
}
```

- [ ] **PoiInteractable — use context.DialogueService**

```csharp
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class PoiInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private PoiData data = null!;

        public void Interact(InteractionContext context)
        {
            context.DialogueService.StartDialogue(this.data.YarnNodeName);
        }
    }
}
```

- [ ] **DoorInteractable — full refactor per spec**

```csharp
#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class DoorInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private DoorData   data   = null!;
        [SerializeField] private UnityEvent onOpen = new();

        private bool unlocked;

        public void Interact(InteractionContext context)
        {
            if (!this.data.Locked || this.unlocked)
            {
                this.onOpen.Invoke();
                return;
            }

            var keyItem = this.data.KeyItem;

            if (keyItem == null)
            {
                context.DialogueService.StartDialogue(
                    this.data.YarnNodeName,
                    new Dictionary<string, object> { ["$outcome"] = "locked" });
                return;
            }

            var outcome = context.InventoryService.TryUseKey(keyItem.ItemId);

            switch (outcome.Result)
            {
                case KeyUseResult.NotFound:
                    context.DialogueService.StartDialogue(
                        this.data.YarnNodeName,
                        new Dictionary<string, object>
                        {
                            ["$outcome"]  = "needs_key",
                            ["$key_name"] = keyItem.DisplayName
                        });
                    break;

                case KeyUseResult.AlreadyDepleted:
                    context.DialogueService.StartDialogue(
                        this.data.YarnNodeName,
                        new Dictionary<string, object> { ["$outcome"] = "locked" });
                    break;

                case KeyUseResult.Success:
                    context.DialogueService.StartDialogue(
                        this.data.YarnNodeName,
                        new Dictionary<string, object>
                        {
                            ["$outcome"]  = "opened",
                            ["$key_name"] = keyItem.DisplayName
                        },
                        onComplete: () =>
                        {
                            this.unlocked = true;
                            this.onOpen.Invoke();
                        });
                    break;

                case KeyUseResult.DepletedAfterUse:
                    context.InventoryService.RemoveItem(outcome.SlotIndex);
                    context.DialogueService.StartDialogue(
                        this.data.YarnNodeName,
                        new Dictionary<string, object>
                        {
                            ["$outcome"]  = "opened",
                            ["$key_name"] = keyItem.DisplayName
                        },
                        onComplete: () =>
                        {
                            this.unlocked = true;
                            this.onOpen.Invoke();
                        });
                    break;
            }
        }
    }
}
```

- [ ] **ItemSocketInteractable — add yarnNodeName, update TryInsert + Interact**

```csharp
#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Dialogue;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class ItemSocketInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private SocketItemData[] requiredItems = System.Array.Empty<SocketItemData>();
        [SerializeField] private UnityEvent       onActivated   = new();
        [SerializeField] private string           yarnNodeName  = "";

        private bool[] inserted = System.Array.Empty<bool>();

        public bool IsActivated { get; private set; }

        public bool CanInsert(ItemData item)
        {
            if (this.IsActivated) return false;
            if (item is not SocketItemData) return false;
            var ins = EnsureInserted();
            for (int i = 0; i < this.requiredItems.Length; i++)
            {
                if (!ins[i] && this.requiredItems[i].ItemId == item.ItemId)
                    return true;
            }
            return false;
        }

        public bool TryInsert(ItemData item, IDialogueService? dialogueService)
        {
            if (this.IsActivated) return false;
            if (item is not SocketItemData) return false;

            var ins = EnsureInserted();
            for (int i = 0; i < this.requiredItems.Length; i++)
            {
                if (ins[i]) continue;
                if (this.requiredItems[i].ItemId != item.ItemId) continue;

                ins[i] = true;
                int filled = CountFilled();

                dialogueService?.StartDialogue(
                    this.yarnNodeName,
                    new Dictionary<string, object>
                    {
                        ["$insert_result"] = "success",
                        ["$item_name"]     = item.DisplayName,
                        ["$slots_filled"]  = filled,
                        ["$slots_total"]   = this.requiredItems.Length
                    });

                if (IsComplete())
                {
                    this.IsActivated = true;
                    this.onActivated.Invoke();
                }

                return true;
            }

            dialogueService?.StartDialogue(
                this.yarnNodeName,
                new Dictionary<string, object>
                {
                    ["$insert_result"] = "wrong_item",
                    ["$item_name"]     = item.DisplayName
                });
            return false;
        }

        public void Interact(InteractionContext context)
        {
            int filled = CountFilled();
            int total  = this.requiredItems.Length;

            context.DialogueService.StartDialogue(
                this.yarnNodeName,
                new Dictionary<string, object>
                {
                    ["$activated"]    = this.IsActivated,
                    ["$slots_filled"] = filled,
                    ["$slots_total"]  = total
                });
        }

        private bool[] EnsureInserted()
        {
            if (this.inserted.Length != this.requiredItems.Length)
                this.inserted = new bool[this.requiredItems.Length];
            return this.inserted;
        }

        private int CountFilled()
        {
            var ins   = EnsureInserted();
            int count = 0;
            for (int i = 0; i < ins.Length; i++)
                if (ins[i]) count++;
            return count;
        }

        private bool IsComplete()
        {
            var ins = EnsureInserted();
            for (int i = 0; i < ins.Length; i++)
                if (!ins[i]) return false;
            return ins.Length > 0;
        }
    }
}
```

- [ ] **NavigationScope — swap registrations**

```csharp
#nullable enable

using VContainer;
using VContainer.Unity;
using UnityEngine;
using Yarn.Unity;
using CrimsonDraft.Infrastructure.Cameras;
using CrimsonDraft.Navigation.Combat;
using CrimsonDraft.Navigation.Dialogue;
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Navigation.Player;
using CrimsonDraft.Navigation.UI;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Navigation
{
    public sealed class NavigationScope : LifetimeScope
    {
        [SerializeField] private StartingLoadout        startingLoadout      = null!;
        [SerializeField] private CombineRecipeLibrary   combineRecipeLibrary = null!;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance(this.startingLoadout);
            builder.RegisterInstance(this.combineRecipeLibrary);
            builder.Register<CombineService>(Lifetime.Singleton).AsSelf().AsImplementedInterfaces();

            builder.RegisterComponentInHierarchy<PlayerController>();
            builder.RegisterComponentInHierarchy<InventoryView>();
            builder.Register<InventoryService>(Lifetime.Singleton).AsSelf().As<IInventoryService>();
            builder.Register<InventoryController>(Lifetime.Scoped).AsImplementedInterfaces();
            builder.Register<InventoryBootstrap>(Lifetime.Singleton).AsImplementedInterfaces();
            builder.RegisterComponentInHierarchy<PlaceholderOverlayView>();
            builder.Register<PlaceholderOverlayController>(Lifetime.Scoped).AsImplementedInterfaces();

            builder.RegisterComponentInHierarchy<CombatTrigger>();
            builder.RegisterComponentInHierarchy<NavigationCameraRegistrar>().AsImplementedInterfaces();
            builder.Register<StartingLoadoutRosterSeedProvider>(Lifetime.Singleton).As<IOperatorRosterSeedProvider>();
            builder.Register<OperatorRoster>(Lifetime.Singleton).AsSelf().As<IOperatorRoster>();
            builder.Register<OperatorRosterBootstrap>(Lifetime.Scoped).AsImplementedInterfaces();

            builder.RegisterComponentInHierarchy<PlayerInteractionCaster>().AsSelf().As<IInteractionCaster>();
            builder.RegisterComponentInHierarchy<DialogueRunner>();
            builder.RegisterComponentInHierarchy<InMemoryVariableStorage>();
            builder.Register<DialogueService>(Lifetime.Scoped).AsSelf().AsImplementedInterfaces();

            builder.RegisterComponentInHierarchy<InteractionReaderView>();
            builder.Register<DocumentController>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
            builder.RegisterComponentInHierarchy<ContainerView>();
            builder.Register<ContainerController>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
        }
    }
}
```

- [ ] **Delete PoiController.cs**

Delete the file: `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/UI/PoiController.cs`

Also delete its `.meta` file if present.

- [ ] **Delete PoiDialogView.cs**

Delete the file: `Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/UI/PoiDialogView.cs`

Also delete its `.meta` file if present.

- [ ] **Update ItemSocketInteractableTests — fix TryInsert call sites**

The parameter was renamed from `poi` to `dialogueService`. Update all call sites from `socket.TryInsert(data, poi: null)` to `socket.TryInsert(data, null)` (positional):

```csharp
// Line 57
bool result = socket.TryInsert(data, null);

// Line 69
bool result = socket.TryInsert(wrong, null);

// Line 81
bool result = socket.TryInsert(consumable, null);

// Line 91 (activation check)
socket.TryInsert(data, null); // activates

// Line 92
bool result = socket.TryInsert(data, null);

// Lines 105-108
socket.TryInsert(dataA, null);
Assert.IsFalse(socket.IsActivated, "still one slot remaining");
socket.TryInsert(dataB, null);
Assert.IsTrue(socket.IsActivated, "all slots satisfied");

// Lines 115-116
bool first  = socket.TryInsert(data, null);
bool second = socket.TryInsert(data, null);

// Lines 132-133
socket.TryInsert(dataA, null);
bool duplicate = socket.TryInsert(dataA, null);
```

- [ ] **Verify compilation in Unity** — zero errors. Check Console for any missing component or reference warnings.

- [ ] **Run existing EditMode tests**

In Unity: Window → General → Test Runner → EditMode → Run All.

Expected: All `ItemSocketInteractableTests` pass. No regressions in other suites.

- [ ] **Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/InteractionContext.cs"
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/PlayerInteractionCaster.cs"
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/PoiInteractable.cs"
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/DoorInteractable.cs"
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/ItemSocketInteractable.cs"
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs"
git add "Game/CrimsonDraft/Assets/Tests/EditMode/ItemSocketInteractableTests.cs"
git rm "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/UI/PoiController.cs"
git rm "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/UI/PoiController.cs.meta"
git rm "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/UI/PoiDialogView.cs"
git rm "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/UI/PoiDialogView.cs.meta"
git commit -m "feat(yarn-spinner): replace PoiController with IDialogueService across all interactables"
```

---

## Task 7: DoorInteractable tests

**Files:**
- Create: `Game/CrimsonDraft/Assets/Tests/EditMode/DoorInteractableTests.cs`

- [ ] **Write failing tests**

```csharp
#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Dialogue;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.Tests
{
    public sealed class DoorInteractableTests
    {
        // ── Helpers ──────────────────────────────────────────────────────────

        private static DoorData MakeDoorData(bool locked, string yarnNodeName, KeyItemData? keyItem = null)
        {
            var data = ScriptableObject.CreateInstance<DoorData>();
            var so   = new SerializedObject(data);
            so.FindProperty("locked").boolValue            = locked;
            so.FindProperty("yarnNodeName").stringValue    = yarnNodeName;
            if (keyItem != null)
                so.FindProperty("keyItem").objectReferenceValue = keyItem;
            so.ApplyModifiedPropertiesWithoutUndo();
            return data;
        }

        private static KeyItemData MakeKeyItemData(string id, string displayName)
        {
            var data = ScriptableObject.CreateInstance<KeyItemData>();
            var so   = new SerializedObject(data);
            so.FindProperty("itemId").stringValue      = id;
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("itemType").enumValueIndex = (int)ItemType.KeyItem;
            so.ApplyModifiedPropertiesWithoutUndo();
            return data;
        }

        private static DoorInteractable MakeDoor(DoorData data)
        {
            var go   = new GameObject();
            var door = go.AddComponent<DoorInteractable>();
            var so   = new SerializedObject(door);
            so.FindProperty("data").objectReferenceValue = data;
            so.ApplyModifiedPropertiesWithoutUndo();
            return door;
        }

        private static InteractionContext MakeContext(
            FakeDoorDialogueService dialogue,
            FakeDoorInventoryService inventory)
        {
            return new InteractionContext(
                inventory,
                null!,
                dialogue,
                null!,
                null!);
        }

        // ── Tests ─────────────────────────────────────────────────────────────

        [Test]
        public void Interact_whenNotLocked_doesNotStartDialogue()
        {
            var data      = MakeDoorData(locked: false, yarnNodeName: "door_test");
            var door      = MakeDoor(data);
            var dialogue  = new FakeDoorDialogueService();
            var inventory = new FakeDoorInventoryService();
            bool opened   = false;

            var so = new SerializedObject(door);
            var onOpen = so.FindProperty("onOpen");
            // We test via IsActivated flag instead — onOpen is a UnityEvent, hard to assert in EditMode.
            // Just verify dialogue was NOT started.

            door.Interact(MakeContext(dialogue, inventory));

            Assert.IsNull(dialogue.LastNodeName, "unlocked door should not start dialogue");
        }

        [Test]
        public void Interact_whenLockedNoKeyItem_startsDialogueWithLockedOutcome()
        {
            var data      = MakeDoorData(locked: true, yarnNodeName: "door_test", keyItem: null);
            var door      = MakeDoor(data);
            var dialogue  = new FakeDoorDialogueService();
            var inventory = new FakeDoorInventoryService();

            door.Interact(MakeContext(dialogue, inventory));

            Assert.AreEqual("door_test", dialogue.LastNodeName);
            Assert.AreEqual("locked", dialogue.LastVariables!["$outcome"]);
        }

        [Test]
        public void Interact_whenLockedKeyNotFound_startsDialogueWithNeedsKeyOutcome()
        {
            var keyData   = MakeKeyItemData("keycard-a", "Keycard A");
            var data      = MakeDoorData(locked: true, yarnNodeName: "door_test", keyItem: keyData);
            var door      = MakeDoor(data);
            var dialogue  = new FakeDoorDialogueService();
            var inventory = new FakeDoorInventoryService
            {
                TryUseKeyResult = new KeyUseOutcome(KeyUseResult.NotFound, -1)
            };

            door.Interact(MakeContext(dialogue, inventory));

            Assert.AreEqual("door_test", dialogue.LastNodeName);
            Assert.AreEqual("needs_key", dialogue.LastVariables!["$outcome"]);
            Assert.AreEqual("Keycard A", dialogue.LastVariables["$key_name"]);
        }

        [Test]
        public void Interact_whenKeySuccess_startsDialogueWithOpenedOutcome_andOnCompleteOpens()
        {
            var keyData   = MakeKeyItemData("keycard-a", "Keycard A");
            var data      = MakeDoorData(locked: true, yarnNodeName: "door_test", keyItem: keyData);
            var door      = MakeDoor(data);
            var dialogue  = new FakeDoorDialogueService();
            var inventory = new FakeDoorInventoryService
            {
                TryUseKeyResult = new KeyUseOutcome(KeyUseResult.Success, 2)
            };

            door.Interact(MakeContext(dialogue, inventory));

            Assert.AreEqual("door_test", dialogue.LastNodeName);
            Assert.AreEqual("opened", dialogue.LastVariables!["$outcome"]);
            Assert.IsNotNull(dialogue.LastOnComplete, "onComplete callback should be set");

            // Simulate dialogue ending
            dialogue.LastOnComplete!.Invoke();

            // After onComplete: door should not start another dialogue on re-interact
            var dialogue2 = new FakeDoorDialogueService();
            door.Interact(MakeContext(dialogue2, inventory));
            Assert.IsNull(dialogue2.LastNodeName, "door is now unlocked, no dialogue");
        }

        [Test]
        public void Interact_whenKeyDepletedAfterUse_removesItemFromInventory()
        {
            var keyData   = MakeKeyItemData("keycard-a", "Keycard A");
            var data      = MakeDoorData(locked: true, yarnNodeName: "door_test", keyItem: keyData);
            var door      = MakeDoor(data);
            var dialogue  = new FakeDoorDialogueService();
            var inventory = new FakeDoorInventoryService
            {
                TryUseKeyResult = new KeyUseOutcome(KeyUseResult.DepletedAfterUse, 3)
            };

            door.Interact(MakeContext(dialogue, inventory));

            Assert.IsTrue(inventory.RemoveItemCalled);
            Assert.AreEqual(3, inventory.RemovedSlotIndex);
        }

        // ── Fakes ─────────────────────────────────────────────────────────────

        private sealed class FakeDoorDialogueService : IDialogueService
        {
            public bool IsRunning => false;
            public string?                              LastNodeName    { get; private set; }
            public IReadOnlyDictionary<string, object>? LastVariables   { get; private set; }
            public System.Action?                       LastOnComplete  { get; private set; }

            public void StartDialogue(
                string                                  nodeName,
                IReadOnlyDictionary<string, object>?   variables  = null,
                System.Action?                          onComplete = null,
                IReadOnlyDictionary<string, Action>?   commands   = null)
            {
                LastNodeName   = nodeName;
                LastVariables  = variables;
                LastOnComplete = onComplete;
            }
        }

        private sealed class FakeDoorInventoryService : IInventoryService
        {
            public KeyUseOutcome TryUseKeyResult = new KeyUseOutcome(KeyUseResult.NotFound, -1);
            public bool          RemoveItemCalled  { get; private set; }
            public int           RemovedSlotIndex  { get; private set; } = -1;

            public System.Collections.Generic.IReadOnlyList<InventorySlot> Slots => System.Array.Empty<InventorySlot>();
            public int  SlotCount                                               => 0;
            public bool AddItem(ItemData data, int operatorSlot, int quantity = 0) => false;
            public bool AddItemAuto(ItemData data, int quantity = 0)               => false;
            public void RemoveItem(int slotIndex) { RemoveItemCalled = true; RemovedSlotIndex = slotIndex; }
            public void MoveItem(int fromSlot, int toSlot)         { }
            public void EquipWeapon(int slotIndex, int operatorSlot) { }
            public void UnequipWeapon(int slotIndex)               { }
            public int  GetEquippedWeaponIndex(int operatorSlot)   => -1;
            public bool CanReload(int slotIndex, int operatorSlot) => false;
            public void ReloadOperator(int slotIndex, int operatorSlot) { }
            public bool TryCombine(int slotA, int slotB)           => false;
            public KeyUseOutcome TryUseKey(string keyItemId)       => TryUseKeyResult;
        }
    }
}
```

- [ ] **Run tests — verify they pass**

Unity → Test Runner → EditMode → Run All.

Expected: All 5 `DoorInteractableTests` pass.

- [ ] **Commit**

```bash
git add "Game/CrimsonDraft/Assets/Tests/EditMode/DoorInteractableTests.cs"
git commit -m "test(yarn-spinner): add DoorInteractableTests covering all key use outcomes"
```

---

## Task 8: Sample .yarn files

**Files:**
- Create: `Game/CrimsonDraft/Assets/Dialogues/poi/poi_test.yarn`
- Create: `Game/CrimsonDraft/Assets/Dialogues/doors/door_test.yarn`
- Create: `Game/CrimsonDraft/Assets/Dialogues/sockets/socket_test.yarn`

These are placeholder nodes for wiring up the scene. Replace with real content as needed.

- [ ] **Create poi_test.yarn**

```yarn
title: poi_test
---
Texto de examinacion de prueba.
===
```

- [ ] **Create door_test.yarn**

```yarn
title: door_test
---
<<if $outcome == "opened">>
Usaste {$key_name}.
<<elseif $outcome == "needs_key">>
Necesitas {$key_name} para abrir esta puerta.
<<else>>
La puerta esta bloqueada.
<<endif>>
===
```

- [ ] **Create socket_test.yarn**

```yarn
title: socket_test
---
<<if $activated>>
Ya esta activado.
<<else>>
Slots completados: {$slots_filled} de {$slots_total}.
<<endif>>
===
```

- [ ] **Commit**

```bash
git add "Game/CrimsonDraft/Assets/Dialogues/"
git commit -m "feat(yarn-spinner): add placeholder .yarn dialogue files"
```

---

## Task 9: Unity Editor scene setup

This task is done manually in the Unity Editor — no code files.

- [ ] **Create YarnProject asset**

In Unity: right-click `Assets/Dialogues/` → Create → Yarn Spinner → Yarn Project. Name it `Navigation`. This creates `Navigation.yarnproject`.

Set the Source Files pattern to include `**/*.yarn` (or select all `.yarn` files manually).

- [ ] **Add DialogueRunner to Navigation scene**

Open `Navigation.unity`. Create an empty GameObject named `DialogueRunner`. Add component: `Dialogue Runner`. In the Inspector, assign `Navigation.yarnproject` to the "Yarn Project" field.

- [ ] **Add InMemoryVariableStorage**

On the same `DialogueRunner` GameObject (or a child), add component: `In Memory Variable Storage`. In the `DialogueRunner`'s Inspector, assign this component to the "Variable Storage" field.

- [ ] **Add Yarn views**

On the `DialogueRunner` GameObject (or a Canvas child), add:
- `Line View` component — Yarn's built-in single-line display
- `Options List View` component — Yarn's built-in option buttons

Wire them to the `DialogueRunner`'s "Dialogue Views" list in the Inspector.

- [ ] **Assign PoiData.yarnNodeName on existing assets**

For each `PoiData` asset in the project: open it in the Inspector and type the Yarn node name (e.g. `poi_test` for the test POI). The old `lines` field is gone.

For each `DoorData` asset: set `yarnNodeName` to `door_test` (for testing). Set the actual node names to match the real `.yarn` files.

For each `ItemSocketInteractable` in the scene: set `yarnNodeName` to `socket_test`.

- [ ] **Smoke test**

Enter Play mode. Walk up to a POI and interact. Verify:
- Game pauses (`Time.timeScale = 0`)
- Yarn dialogue panel appears with the node's text
- Pressing UIConfirm advances or closes the dialogue
- After closing, `Time.timeScale` returns to 1 and Gameplay input resumes

Walk up to a locked door with a key. Interact. Verify:
- Yarn node runs with `$outcome = "opened"` or `"needs_key"` depending on inventory
- Door opens after dialogue closes (if key was used)

- [ ] **Commit scene changes**

```bash
git add "Game/CrimsonDraft/Assets/Scenes/Navigation.unity"
git add "Game/CrimsonDraft/Assets/Scenes/Navigation.unity.meta"
git add "Game/CrimsonDraft/Assets/Dialogues/"
git commit -m "feat(yarn-spinner): wire DialogueRunner and Yarn views in Navigation scene"
```

---

## Self-Review

**Spec coverage check:**

| Spec requirement | Task |
|---|---|
| Install YarnSpinner-Unity | Task 1 |
| YarnProject at Assets/Dialogues/ | Task 9 |
| IDialogueService interface | Task 3 |
| DialogueService — pause, switch input, variables, commands, onComplete | Task 4 |
| InMemoryVariableStorage.Clear + SetValue | Task 4 |
| One-time command handler cleanup | Task 4 |
| InteractionContext: PoiController → IDialogueService | Task 6 |
| Delete PoiController + PoiDialogView | Task 6 |
| PoiData: lines → yarnNodeName | Task 5 |
| DoorData: add yarnNodeName | Task 5 |
| ItemSocketInteractable: add yarnNodeName | Task 6 |
| PoiInteractable refactor | Task 6 |
| DoorInteractable full refactor (4 outcomes, onComplete) | Task 6 |
| ItemSocketInteractable TryInsert + Interact | Task 6 |
| NavigationScope swap registrations | Task 6 |
| asmdef references | Task 2 |
| Sample .yarn files | Task 8 |
| Scene setup | Task 9 |
| Tests | Tasks 6 + 7 |
