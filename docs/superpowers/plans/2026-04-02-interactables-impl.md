# Sistema de Interactuables — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Spec:** Implements [[Sistema de Interactuables]]

**Goal:** Build a raycast-based interactable system with 5 concrete types (Pickup, Document, Door, POI, Container) plus demo objects in the Navigation scene.

**Architecture:** `PlayerInteractionCaster` fires a forward raycast on `Interact.performed` against the "Interactable" layer and calls `IInteractable.Interact(InteractionContext)` on the first hit. Each type is a self-contained MonoBehaviour. UI controllers (non-MonoBehaviour, VContainer-managed) handle pause/input-switching/view lifecycle.

**Tech Stack:** Unity 6, C# 9, VContainer, Unity Input System, TextMeshPro, Unity Physics

---

## File Map

**New files — Scripts:**
- `Assets/Scripts/Navigation/Interactables/IInteractable.cs`
- `Assets/Scripts/Navigation/Interactables/InteractionContext.cs`
- `Assets/Scripts/Navigation/Interactables/PlayerInteractionCaster.cs`
- `Assets/Scripts/Navigation/Interactables/PickupInteractable.cs`
- `Assets/Scripts/Navigation/Interactables/DocumentInteractable.cs`
- `Assets/Scripts/Navigation/Interactables/DoorInteractable.cs`
- `Assets/Scripts/Navigation/Interactables/PoiInteractable.cs`
- `Assets/Scripts/Navigation/Interactables/ContainerInteractable.cs`
- `Assets/Scripts/Navigation/Interactables/Data/DocumentData.cs`
- `Assets/Scripts/Navigation/Interactables/Data/PoiData.cs`
- `Assets/Scripts/Navigation/Interactables/Data/DoorData.cs`
- `Assets/Scripts/Navigation/Interactables/Data/ContainerData.cs`
- `Assets/Scripts/Navigation/Interactables/UI/InteractionReaderView.cs`
- `Assets/Scripts/Navigation/Interactables/UI/PoiDialogView.cs`
- `Assets/Scripts/Navigation/Interactables/UI/ContainerView.cs`
- `Assets/Scripts/Navigation/Interactables/UI/DocumentController.cs`
- `Assets/Scripts/Navigation/Interactables/UI/PoiController.cs`
- `Assets/Scripts/Navigation/Interactables/UI/ContainerController.cs`

**Modified files:**
- `Assets/Scripts/Navigation/NavigationScope.cs` — register new controllers and views
- `Assets/Input/CrimsonDraftControls.inputactions` — already has Interact action (no change needed)

---

## Task 1: Layer + Interface + Context

**Files:**
- Create: `Assets/Scripts/Navigation/Interactables/IInteractable.cs`
- Create: `Assets/Scripts/Navigation/Interactables/InteractionContext.cs`

- [ ] **Step 1: Add the "Interactable" layer in Unity**

  In Unity Editor: Edit → Project Settings → Tags and Layers → add layer named `Interactable` in the first available slot (typically layer 8). Note the layer index — you'll need it to configure colliders.

- [ ] **Step 2: Create `IInteractable`**

```csharp
// Assets/Scripts/Navigation/Interactables/IInteractable.cs
#nullable enable

namespace CrimsonDraft.Navigation.Interactables
{
    public interface IInteractable
    {
        void Interact(InteractionContext context);
    }
}
```

- [ ] **Step 3: Create `InteractionContext`**

```csharp
// Assets/Scripts/Navigation/Interactables/InteractionContext.cs
#nullable enable

using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class InteractionContext
    {
        public readonly IInventoryService InventoryService;
        public readonly IInputService     InputService;

        public InteractionContext(IInventoryService inventoryService, IInputService inputService)
        {
            InventoryService = inventoryService;
            InputService     = inputService;
        }
    }
}
```

- [ ] **Step 4: Check console for compilation errors**

  In Unity Editor, check Console window. No errors expected — these are new files with no dependencies yet.

- [ ] **Step 5: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/IInteractable.cs" \
        "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/InteractionContext.cs"
git commit -m "feat(navigation): add IInteractable interface and InteractionContext"
```

---

## Task 2: PlayerInteractionCaster

**Files:**
- Create: `Assets/Scripts/Navigation/Interactables/PlayerInteractionCaster.cs`

- [ ] **Step 1: Create `PlayerInteractionCaster`**

```csharp
// Assets/Scripts/Navigation/Interactables/PlayerInteractionCaster.cs
#nullable enable

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;
using VContainer;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class PlayerInteractionCaster : MonoBehaviour
    {
        [SerializeField] private float     rayDistance = 2f;
        [SerializeField] private LayerMask interactableLayer;

        private IInputService     inputService     = null!;
        private IInventoryService inventoryService = null!;

        [Inject]
        public void Construct(IInputService inputService, IInventoryService inventoryService)
        {
            this.inputService     = inputService;
            this.inventoryService = inventoryService;
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

            var context = new InteractionContext(this.inventoryService, this.inputService);
            interactable.Interact(context);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, transform.forward * this.rayDistance);
        }
#endif
    }
}
```

- [ ] **Step 2: Add `PlayerInteractionCaster` to the Player GameObject in the scene**

  In Unity: select the Player GameObject → Add Component → `PlayerInteractionCaster`.
  - Set `Ray Distance` to `2`
  - Set `Interactable Layer` mask to only the `Interactable` layer

- [ ] **Step 3: Verify in Scene view**

  Select Player in the scene. The cyan gizmo ray should be visible in the Scene view pointing forward. Play the scene and press C — no errors should appear in Console.

- [ ] **Step 4: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/PlayerInteractionCaster.cs"
git commit -m "feat(navigation): add PlayerInteractionCaster with forward raycast"
```

---

## Task 3: ScriptableObject data types

**Files:**
- Create: `Assets/Scripts/Navigation/Interactables/Data/DocumentData.cs`
- Create: `Assets/Scripts/Navigation/Interactables/Data/PoiData.cs`
- Create: `Assets/Scripts/Navigation/Interactables/Data/DoorData.cs`
- Create: `Assets/Scripts/Navigation/Interactables/Data/ContainerData.cs`

- [ ] **Step 1: Create `DocumentData`**

```csharp
// Assets/Scripts/Navigation/Interactables/Data/DocumentData.cs
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    [CreateAssetMenu(menuName = "CrimsonDraft/Interactables/DocumentData")]
    public sealed class DocumentData : ScriptableObject
    {
        [SerializeField] private string   title = string.Empty;
        [SerializeField] private string[] pages = System.Array.Empty<string>();

        public string   Title => this.title;
        public string[] Pages => this.pages;
    }
}
```

- [ ] **Step 2: Create `PoiData`**

```csharp
// Assets/Scripts/Navigation/Interactables/Data/PoiData.cs
#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    [CreateAssetMenu(menuName = "CrimsonDraft/Interactables/PoiData")]
    public sealed class PoiData : ScriptableObject
    {
        [SerializeField] private string[] lines = System.Array.Empty<string>();

        public string[] Lines => this.lines;
    }
}
```

- [ ] **Step 3: Create `DoorData`**

```csharp
// Assets/Scripts/Navigation/Interactables/Data/DoorData.cs
#nullable enable

using UnityEngine;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Interactables
{
    [CreateAssetMenu(menuName = "CrimsonDraft/Interactables/DoorData")]
    public sealed class DoorData : ScriptableObject
    {
        [SerializeField] private bool      locked  = false;
        [SerializeField] private ItemData? keyItem = null;

        public bool      Locked  => this.locked;
        public ItemData? KeyItem => this.keyItem;
    }
}
```

- [ ] **Step 4: Create `ContainerData`**

```csharp
// Assets/Scripts/Navigation/Interactables/Data/ContainerData.cs
#nullable enable

using UnityEngine;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Interactables
{
    [CreateAssetMenu(menuName = "CrimsonDraft/Interactables/ContainerData")]
    public sealed class ContainerData : ScriptableObject
    {
        [SerializeField] private ItemData[] items   = System.Array.Empty<ItemData>();
        [SerializeField] private bool       emptied = false;

        public ItemData[] Items   => this.items;
        public bool       Emptied { get => this.emptied; set => this.emptied = value; }
    }
}
```

- [ ] **Step 5: Check console for compilation errors**

  Unity Console should show zero errors after domain reload.

- [ ] **Step 6: Verify CreateAssetMenu entries appear**

  Right-click in Project window → Create → CrimsonDraft → Interactables → should show DocumentData, PoiData, DoorData, ContainerData.

- [ ] **Step 7: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/Data/"
git commit -m "feat(navigation): add interactable ScriptableObject data types"
```

---

## Task 4: PickupInteractable

**Files:**
- Create: `Assets/Scripts/Navigation/Interactables/PickupInteractable.cs`

- [ ] **Step 1: Create `PickupInteractable`**

```csharp
// Assets/Scripts/Navigation/Interactables/PickupInteractable.cs
#nullable enable

using UnityEngine;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class PickupInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private ItemData item = null!;

        public void Interact(InteractionContext context)
        {
            context.InventoryService.AddItem(this.item);
            gameObject.SetActive(false);
        }
    }
}
```

- [ ] **Step 2: Create the demo pickup in the Navigation scene**

  In Unity:
  1. Create an empty GameObject named `Pickup_Weapon_Demo`
  2. Add a Sphere (or any visible mesh) as child for visibility
  3. Add component `PickupInteractable`
  4. Assign any existing `WeaponData` asset to the `Item` field
  5. Add a `SphereCollider` (Is Trigger: false) — set its layer to `Interactable`
  6. Position it somewhere reachable from the Player start position

- [ ] **Step 3: Play-test**

  Press Play. Walk the player toward the pickup object, face it, press C. The object should disappear and the item should appear in the inventory (open inventory with Z to verify).

- [ ] **Step 4: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/PickupInteractable.cs"
git commit -m "feat(navigation): add PickupInteractable"
```

---

## Task 5: UI Views — PoiDialogView

**Files:**
- Create: `Assets/Scripts/Navigation/Interactables/UI/PoiDialogView.cs`

- [ ] **Step 1: Create `PoiDialogView` script**

```csharp
// Assets/Scripts/Navigation/Interactables/UI/PoiDialogView.cs
#nullable enable

using TMPro;
using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables.UI
{
    public sealed class PoiDialogView : MonoBehaviour
    {
        [SerializeField] private GameObject      panel = null!;
        [SerializeField] private TextMeshProUGUI label = null!;

        public void Show(string line)
        {
            this.label.text = line;
            this.panel.SetActive(true);
        }

        public void Hide() => this.panel.SetActive(false);
    }
}
```

- [ ] **Step 2: Build the PoiDialogView Canvas hierarchy**

  In Unity Navigation scene, under the existing Canvas:
  1. Create child GameObject `PoiDialogPanel` (RectTransform anchored to bottom-center, height ~120px, full width)
  2. Add a semi-transparent Image background
  3. Add a TextMeshProUGUI child `Label` (centered, font size ~24)
  4. Add `PoiDialogView` component to `PoiDialogPanel`
  5. Assign `Panel` = `PoiDialogPanel`, `Label` = the TextMeshProUGUI
  6. Set `PoiDialogPanel` inactive by default

- [ ] **Step 3: Check compilation, then commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/UI/PoiDialogView.cs"
git commit -m "feat(navigation): add PoiDialogView"
```

---

## Task 6: UI Views — InteractionReaderView

**Files:**
- Create: `Assets/Scripts/Navigation/Interactables/UI/InteractionReaderView.cs`

- [ ] **Step 1: Create `InteractionReaderView` script**

```csharp
// Assets/Scripts/Navigation/Interactables/UI/InteractionReaderView.cs
#nullable enable

using TMPro;
using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables.UI
{
    public sealed class InteractionReaderView : MonoBehaviour
    {
        [SerializeField] private GameObject      panel      = null!;
        [SerializeField] private TextMeshProUGUI titleLabel = null!;
        [SerializeField] private TextMeshProUGUI bodyLabel  = null!;
        [SerializeField] private GameObject      prevHint   = null!;
        [SerializeField] private GameObject      nextHint   = null!;

        public void Show(string title, string pageText, bool hasPrev, bool hasNext)
        {
            this.titleLabel.text = title;
            this.bodyLabel.text  = pageText;
            this.prevHint.SetActive(hasPrev);
            this.nextHint.SetActive(hasNext);
            this.panel.SetActive(true);
        }

        public void Hide() => this.panel.SetActive(false);
    }
}
```

- [ ] **Step 2: Build the InteractionReaderView Canvas hierarchy**

  In Unity, under the existing Canvas:
  1. Create child GameObject `ReaderPanel` (RectTransform stretch-fill the entire screen)
  2. Add a dark semi-transparent Image background
  3. Add TextMeshProUGUI `TitleLabel` (top area, large font)
  4. Add TextMeshProUGUI `BodyLabel` (center area, scrollable area)
  5. Add two small GameObjects `PrevHint` and `NextHint` (bottom corners, TextMeshPro with "← Anterior" / "Siguiente →")
  6. Add `InteractionReaderView` component to `ReaderPanel`
  7. Assign all serialized fields
  8. Set `ReaderPanel` inactive by default

- [ ] **Step 3: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/UI/InteractionReaderView.cs"
git commit -m "feat(navigation): add InteractionReaderView"
```

---

## Task 7: UI Views — ContainerView

**Files:**
- Create: `Assets/Scripts/Navigation/Interactables/UI/ContainerView.cs`

- [ ] **Step 1: Create `ContainerView` script**

```csharp
// Assets/Scripts/Navigation/Interactables/UI/ContainerView.cs
#nullable enable

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Interactables.UI
{
    public sealed class ContainerView : MonoBehaviour
    {
        [SerializeField] private GameObject      panel           = null!;
        [SerializeField] private Transform       itemListParent  = null!;
        [SerializeField] private TextMeshProUGUI itemRowPrefab   = null!;

        private readonly List<TextMeshProUGUI> rows = new();

        public void Show(IReadOnlyList<ItemData> items, int cursorIndex)
        {
            while (this.rows.Count < items.Count)
                this.rows.Add(Instantiate(this.itemRowPrefab, this.itemListParent));

            for (int i = items.Count; i < this.rows.Count; i++)
                this.rows[i].gameObject.SetActive(false);

            for (int i = 0; i < items.Count; i++)
            {
                this.rows[i].text = i == cursorIndex
                    ? $"> {items[i].DisplayName}"
                    : $"  {items[i].DisplayName}";
                this.rows[i].gameObject.SetActive(true);
            }

            this.panel.SetActive(true);
        }

        public void Hide() => this.panel.SetActive(false);
    }
}
```

- [ ] **Step 2: Build the ContainerView Canvas hierarchy**

  In Unity, under the existing Canvas:
  1. Create child GameObject `ContainerPanel` (RectTransform anchored right side, roughly half screen width)
  2. Add semi-transparent Image background
  3. Add a TextMeshProUGUI title "CONTENEDOR" at top
  4. Create an empty `ItemListParent` container for rows
  5. Create a `ItemRowPrefab` TextMeshProUGUI in the Project (not in scene — drag to field)
  6. Add `ContainerView` to `ContainerPanel`, assign fields
  7. Set `ContainerPanel` inactive by default

- [ ] **Step 3: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/UI/ContainerView.cs"
git commit -m "feat(navigation): add ContainerView"
```

---

## Task 8: PoiController + PoiInteractable

**Files:**
- Create: `Assets/Scripts/Navigation/Interactables/UI/PoiController.cs`
- Create: `Assets/Scripts/Navigation/Interactables/PoiInteractable.cs`

- [ ] **Step 1: Create `PoiController`**

```csharp
// Assets/Scripts/Navigation/Interactables/UI/PoiController.cs
#nullable enable

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Navigation.Interactables.UI;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class PoiController : IInitializable, IDisposable
    {
        private readonly IInputService inputService;
        private readonly PoiDialogView view;

        private string[] lines = Array.Empty<string>();
        private int      lineIndex;
        private bool     isOpen;

        [Preserve]
        public PoiController(IInputService inputService, PoiDialogView view)
        {
            this.inputService = inputService;
            this.view         = view;
        }

        void IInitializable.Initialize()
        {
            this.inputService.Interact.performed += OnInteract;
        }

        public void Open(string[] poiLines)
        {
            this.lines     = poiLines;
            this.lineIndex = 0;
            this.isOpen    = true;
            Time.timeScale  = 0f;
            this.inputService.SwitchToUI();
            this.view.Show(this.lines[0]);
        }

        private void OnInteract(InputAction.CallbackContext _)
        {
            if (!this.isOpen) return;

            this.lineIndex++;

            if (this.lineIndex >= this.lines.Length)
            {
                Close();
                return;
            }

            this.view.Show(this.lines[this.lineIndex]);
        }

        private void Close()
        {
            this.isOpen = false;
            this.view.Hide();
            Time.timeScale = 1f;
            this.inputService.SwitchToGameplay();
        }

        void IDisposable.Dispose()
        {
            this.inputService.Interact.performed -= OnInteract;
        }
    }
}
```

- [ ] **Step 2: Create `PoiInteractable`**

```csharp
// Assets/Scripts/Navigation/Interactables/PoiInteractable.cs
#nullable enable

using UnityEngine;
using VContainer;
using CrimsonDraft.Navigation.Interactables.UI;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class PoiInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private PoiData data = null!;

        private PoiController controller = null!;

        [Inject]
        public void Construct(PoiController controller)
        {
            this.controller = controller;
        }

        public void Interact(InteractionContext context)
        {
            this.controller.Open(this.data.Lines);
        }
    }
}
```

- [ ] **Step 3: Register in `NavigationScope`**

  Open `Assets/Scripts/Navigation/NavigationScope.cs` and add inside `Configure`:

```csharp
builder.RegisterComponentInHierarchy<PoiDialogView>();
builder.Register<PoiController>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
```

  Also add the using if needed:
```csharp
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Navigation.Interactables.UI;
```

- [ ] **Step 4: Create demo POI in Navigation scene**

  1. Create empty GameObject `POI_BloodTrail_Demo`
  2. Add `PoiInteractable` component
  3. Create a `PoiData` asset: right-click Project → Create → CrimsonDraft/Interactables/PoiData. Name it `POI_BloodTrail`. Set 3 lines:
     - `"Rastro de sangre. Empieza en la cubierta y llega hasta la borda."`
     - `"Marcas de arrastre. Alguien fue llevado, o se arrastró solo."`
     - `"En la baranda: marcas de manos ensangrentadas."`
  4. Assign the PoiData asset to the `PoiInteractable` component
  5. Add a `BoxCollider` (Is Trigger: false), set layer to `Interactable`
  6. Position it in the scene

- [ ] **Step 5: Play-test**

  Walk to the POI, face it, press C. The bottom panel should appear with the first line. Press C again — second line. Press C again — third line. Press C again — panel closes. Game time should pause while the panel is open (check that the Player doesn't move).

- [ ] **Step 6: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/UI/PoiController.cs" \
        "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/PoiInteractable.cs" \
        "Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs"
git commit -m "feat(navigation): add PoiController and PoiInteractable"
```

---

## Task 9: DocumentController + DocumentInteractable

**Files:**
- Create: `Assets/Scripts/Navigation/Interactables/UI/DocumentController.cs`
- Create: `Assets/Scripts/Navigation/Interactables/DocumentInteractable.cs`

- [ ] **Step 1: Create `DocumentController`**

```csharp
// Assets/Scripts/Navigation/Interactables/UI/DocumentController.cs
#nullable enable

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Navigation.Interactables.UI;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class DocumentController : IInitializable, IDisposable
    {
        private readonly IInputService        inputService;
        private readonly InteractionReaderView view;

        private string[] pages = Array.Empty<string>();
        private string   title = string.Empty;
        private int      pageIndex;
        private bool     isOpen;

        [Preserve]
        public DocumentController(IInputService inputService, InteractionReaderView view)
        {
            this.inputService = inputService;
            this.view         = view;
        }

        void IInitializable.Initialize()
        {
            this.inputService.UINavigate.performed += OnNavigate;
            this.inputService.UIBack.performed     += OnBack;
        }

        public void Open(string docTitle, string[] docPages)
        {
            this.title     = docTitle;
            this.pages     = docPages;
            this.pageIndex = 0;
            this.isOpen    = true;
            Time.timeScale  = 0f;
            this.inputService.SwitchToUI();
            RefreshView();
        }

        private void OnNavigate(InputAction.CallbackContext ctx)
        {
            if (!this.isOpen) return;

            var dir = ctx.ReadValue<Vector2>();
            if (dir.x > 0.5f)
                TryAdvance();
            else if (dir.x < -0.5f)
                TryRetreat();
        }

        private void TryAdvance()
        {
            if (this.pageIndex >= this.pages.Length - 1) return;
            this.pageIndex++;
            RefreshView();
        }

        private void TryRetreat()
        {
            if (this.pageIndex <= 0) return;
            this.pageIndex--;
            RefreshView();
        }

        private void OnBack(InputAction.CallbackContext _)
        {
            if (!this.isOpen) return;
            Close();
        }

        private void Close()
        {
            this.isOpen = false;
            this.view.Hide();
            Time.timeScale = 1f;
            this.inputService.SwitchToGameplay();
        }

        private void RefreshView()
        {
            this.view.Show(
                this.title,
                this.pages[this.pageIndex],
                hasPrev: this.pageIndex > 0,
                hasNext: this.pageIndex < this.pages.Length - 1);
        }

        void IDisposable.Dispose()
        {
            this.inputService.UINavigate.performed -= OnNavigate;
            this.inputService.UIBack.performed     -= OnBack;
        }
    }
}
```

- [ ] **Step 2: Create `DocumentInteractable`**

```csharp
// Assets/Scripts/Navigation/Interactables/DocumentInteractable.cs
#nullable enable

using UnityEngine;
using VContainer;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class DocumentInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private DocumentData data = null!;

        private DocumentController controller = null!;

        [Inject]
        public void Construct(DocumentController controller)
        {
            this.controller = controller;
        }

        public void Interact(InteractionContext context)
        {
            this.controller.Open(this.data.Title, this.data.Pages);
        }
    }
}
```

- [ ] **Step 3: Register in `NavigationScope`**

  Add inside `Configure`:

```csharp
builder.RegisterComponentInHierarchy<InteractionReaderView>();
builder.Register<DocumentController>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
```

- [ ] **Step 4: Create demo document in Navigation scene**

  1. Create empty GameObject `Document_FILE01_Demo`
  2. Add `DocumentInteractable` component
  3. Create a `DocumentData` asset: right-click → Create → CrimsonDraft/Interactables/DocumentData. Name it `Doc_FILE01`.
     - Title: `FILE 01 — "Bitácora Nutricional – Sujeto M12"`
     - Pages[0]: `"No sé quién diseñó esta variante, pero no estaba pensada para humanos comunes.\n\nAl principio creí que era una mejora del compuesto base. Más estable. Más limpio."`
     - Pages[1]: `"M12 pidió comida tres veces en una hora. Después vomitó. Después volvió a pedir comida.\n\nNo había ansiedad. No había estrés. Solo hambre."`
     - Pages[2]: `"A las 72 horas, los suministros de la cubierta inferior estaban vacíos.\n\nEl problema no era agresividad.\n\nEra consumo."`
  4. Assign asset to `DocumentInteractable`
  5. Add `BoxCollider`, set layer to `Interactable`

- [ ] **Step 5: Play-test**

  Walk to the document, press C. Full-screen reader opens. Navigate ← → to flip pages. Press V (UIBack) to close. Game should be paused while open.

- [ ] **Step 6: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/UI/DocumentController.cs" \
        "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/DocumentInteractable.cs" \
        "Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs"
git commit -m "feat(navigation): add DocumentController and DocumentInteractable"
```

---

## Task 10: DoorInteractable

**Files:**
- Create: `Assets/Scripts/Navigation/Interactables/DoorInteractable.cs`

- [ ] **Step 1: Create `DoorInteractable`**

```csharp
// Assets/Scripts/Navigation/Interactables/DoorInteractable.cs
#nullable enable

using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using VContainer;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class DoorInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private DoorData    data    = null!;
        [SerializeField] private UnityEvent  onOpen  = new();

        private PoiController poiController = null!;

        [Inject]
        public void Construct(PoiController poiController)
        {
            this.poiController = poiController;
        }

        public void Interact(InteractionContext context)
        {
            if (!this.data.Locked)
            {
                this.onOpen.Invoke();
                return;
            }

            if (this.data.KeyItem == null)
            {
                this.poiController.Open(new[] { "Bloqueada." });
                return;
            }

            var keyItem = this.data.KeyItem;
            bool hasKey = context.InventoryService.Items
                .Any(item => item.Data.ItemId == keyItem.ItemId);

            if (!hasKey)
            {
                this.poiController.Open(new[] { $"Necesitas: {keyItem.DisplayName}." });
                return;
            }

            var itemIndex = context.InventoryService.Items
                .Select((item, i) => (item, i))
                .First(t => t.item.Data.ItemId == keyItem.ItemId).i;

            context.InventoryService.RemoveItem(itemIndex);
            this.onOpen.Invoke();
        }
    }
}
```

- [ ] **Step 2: Add `RemoveItem` to `IInventoryService` and `InventoryService`**

  Open `Assets/Scripts/Inventory/IInventoryService.cs` and add:

```csharp
/// <summary>Removes item at itemIndex from inventory.</summary>
void RemoveItem(int itemIndex);
```

  Open `Assets/Scripts/Inventory/InventoryService.cs` and add the implementation:

```csharp
public void RemoveItem(int itemIndex)
{
    if (itemIndex < 0 || itemIndex >= this.items.Count)
        throw new System.ArgumentOutOfRangeException(nameof(itemIndex));
    this.items.RemoveAt(itemIndex);
}
```

- [ ] **Step 3: Register in `NavigationScope`** — no change needed, `PoiController` already registered in Task 8.

- [ ] **Step 4: Create demo doors in Navigation scene**

  **Free door:**
  1. Create empty GameObject `Door_Free_Demo`
  2. Add `DoorInteractable` component
  3. Create `DoorData` asset `Door_Free`: locked=false, keyItem=none
  4. On `OnOpen` UnityEvent: wire to any visible action (e.g., disable a wall GameObject)
  5. Add `BoxCollider`, set layer to `Interactable`

  **Locked door:**
  1. Create empty GameObject `Door_Locked_Demo`
  2. Add `DoorInteractable` component
  3. Create `DoorData` asset `Door_Locked`: locked=true, keyItem=assign any `ConsumableData` asset named "Llave"
  4. Create a `ConsumableData` asset `Key_Demo` with itemId=`key_demo`, displayName=`Llave`
  5. Place a `PickupInteractable` nearby referencing `Key_Demo` so the player can obtain it
  6. Add `BoxCollider`, set layer to `Interactable`

- [ ] **Step 5: Play-test**

  - Walk to free door → press C → OnOpen fires.
  - Walk to locked door without key → press C → POI dialog shows "Necesitas: Llave."
  - Pick up the key → walk to locked door → press C → OnOpen fires, key consumed from inventory.

- [ ] **Step 6: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/DoorInteractable.cs" \
        "Game/CrimsonDraft/Assets/Scripts/Inventory/IInventoryService.cs" \
        "Game/CrimsonDraft/Assets/Scripts/Inventory/InventoryService.cs"
git commit -m "feat(navigation): add DoorInteractable with key-lock support"
```

---

## Task 11: ContainerController + ContainerInteractable

**Files:**
- Create: `Assets/Scripts/Navigation/Interactables/UI/ContainerController.cs`
- Create: `Assets/Scripts/Navigation/Interactables/ContainerInteractable.cs`

- [ ] **Step 1: Create `ContainerController`**

```csharp
// Assets/Scripts/Navigation/Interactables/UI/ContainerController.cs
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Interactables.UI;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class ContainerController : IInitializable, IDisposable
    {
        private readonly IInputService    inputService;
        private readonly ContainerView    view;

        private List<ItemData>    containerItems = new();
        private IInventoryService inventoryService = null!;
        private int               cursorIndex;
        private bool              isOpen;

        [Preserve]
        public ContainerController(IInputService inputService, ContainerView view)
        {
            this.inputService = inputService;
            this.view         = view;
        }

        void IInitializable.Initialize()
        {
            this.inputService.UINavigate.performed += OnNavigate;
            this.inputService.UIConfirm.performed  += OnConfirm;
            this.inputService.UIBack.performed     += OnBack;
        }

        public void Open(ContainerData data, IInventoryService inventory)
        {
            if (data.Emptied) return;

            this.inventoryService = inventory;
            this.containerItems   = data.Items.ToList();
            this.cursorIndex      = 0;
            this.isOpen           = true;
            Time.timeScale         = 0f;
            this.inputService.SwitchToUI();
            this.view.Show(this.containerItems, this.cursorIndex);
        }

        private void OnNavigate(InputAction.CallbackContext ctx)
        {
            if (!this.isOpen || this.containerItems.Count == 0) return;

            var dir = ctx.ReadValue<Vector2>();
            int delta = dir.y > 0.5f ? -1 : dir.y < -0.5f ? 1 : 0;
            if (delta == 0) return;

            this.cursorIndex = (this.cursorIndex + delta + this.containerItems.Count) % this.containerItems.Count;
            this.view.Show(this.containerItems, this.cursorIndex);
        }

        private void OnConfirm(InputAction.CallbackContext _)
        {
            if (!this.isOpen || this.containerItems.Count == 0) return;

            var item = this.containerItems[this.cursorIndex];
            this.inventoryService.AddItem(item);
            this.containerItems.RemoveAt(this.cursorIndex);

            if (this.containerItems.Count == 0)
            {
                Close();
                return;
            }

            this.cursorIndex = Mathf.Min(this.cursorIndex, this.containerItems.Count - 1);
            this.view.Show(this.containerItems, this.cursorIndex);
        }

        private void OnBack(InputAction.CallbackContext _)
        {
            if (!this.isOpen) return;
            Close();
        }

        private void Close()
        {
            this.isOpen = false;
            this.view.Hide();
            Time.timeScale = 1f;
            this.inputService.SwitchToGameplay();
        }

        void IDisposable.Dispose()
        {
            this.inputService.UINavigate.performed -= OnNavigate;
            this.inputService.UIConfirm.performed  -= OnConfirm;
            this.inputService.UIBack.performed     -= OnBack;
        }
    }
}
```

- [ ] **Step 2: Create `ContainerInteractable`**

```csharp
// Assets/Scripts/Navigation/Interactables/ContainerInteractable.cs
#nullable enable

using UnityEngine;
using VContainer;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class ContainerInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private ContainerData data = null!;

        private ContainerController controller = null!;

        [Inject]
        public void Construct(ContainerController controller)
        {
            this.controller = controller;
        }

        public void Interact(InteractionContext context)
        {
            this.controller.Open(this.data, context.InventoryService);
        }
    }
}
```

- [ ] **Step 3: Register in `NavigationScope`**

  Add inside `Configure`:

```csharp
builder.RegisterComponentInHierarchy<ContainerView>();
builder.Register<ContainerController>(Lifetime.Scoped).AsImplementedInterfaces().AsSelf();
```

- [ ] **Step 4: Create demo container in Navigation scene**

  1. Create empty GameObject `Container_Box_Demo`
  2. Add `ContainerInteractable` component
  3. Create `ContainerData` asset `Container_Box`: items = [any WeaponData, any ConsumableData], emptied=false
  4. Assign to component
  5. Add `BoxCollider`, set layer to `Interactable`

- [ ] **Step 5: Play-test**

  Walk to the container, press C. ContainerPanel opens alongside inventory. Navigate up/down, press Confirm (Space/Enter/South) to take items. Items should appear in inventory. When container is empty it should close automatically. Press V/West to close early.

- [ ] **Step 6: Commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/UI/ContainerController.cs" \
        "Game/CrimsonDraft/Assets/Scripts/Navigation/Interactables/ContainerInteractable.cs" \
        "Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs"
git commit -m "feat(navigation): add ContainerController and ContainerInteractable"
```

---

## Task 12: Final wiring + scene save

- [ ] **Step 1: Register `PlayerInteractionCaster` in `NavigationScope`**

  Add inside `Configure`:

```csharp
builder.RegisterComponentInHierarchy<PlayerInteractionCaster>();
```

- [ ] **Step 2: Verify all 6 demo objects are in the scene**

  Checklist:
  - [ ] `Pickup_Weapon_Demo` — PickupInteractable, BoxCollider on Interactable layer
  - [ ] `Document_FILE01_Demo` — DocumentInteractable, BoxCollider on Interactable layer
  - [ ] `Door_Free_Demo` — DoorInteractable (unlocked), BoxCollider on Interactable layer
  - [ ] `Door_Locked_Demo` — DoorInteractable (locked), with key pickup nearby
  - [ ] `POI_BloodTrail_Demo` — PoiInteractable, BoxCollider on Interactable layer
  - [ ] `Container_Box_Demo` — ContainerInteractable, BoxCollider on Interactable layer

- [ ] **Step 3: Save the scene**

  Ctrl+S in Unity Editor to save Navigation.unity.

- [ ] **Step 4: Final play-test — test all 5 types in sequence**

  1. Pick up weapon → inventory shows it
  2. Read FILE 01 document → 3 pages, closes with V
  3. Open free door → OnOpen fires
  4. Try locked door without key → feedback shown
  5. Pick up the key → try locked door → opens, key consumed
  6. Open container → take both items, closes when empty

- [ ] **Step 5: Final commit**

```bash
git add "Game/CrimsonDraft/Assets/Scripts/Navigation/NavigationScope.cs" \
        "Game/CrimsonDraft/Assets/Scenes/Navigation.unity"
git commit -m "feat(navigation): wire interactables system and save demo scene"
```

---

## Self-Review

**Spec coverage check:**
- [x] `IInteractable` + `InteractionContext` — Task 1
- [x] `PlayerInteractionCaster` raycast — Task 2
- [x] Layer "Interactable" — Task 1 Step 1
- [x] `PickupInteractable` — Task 4
- [x] `DocumentInteractable` + `InteractionReaderView` — Tasks 6, 9
- [x] `DoorInteractable` free + locked — Task 10
- [x] `PoiInteractable` + `PoiDialogView` — Tasks 5, 8
- [x] `ContainerInteractable` + `ContainerView` — Tasks 7, 11
- [x] All ScriptableObjects — Task 3
- [x] Demo scene with one of each — Tasks 4, 8, 9, 10, 11, 12
- [x] `RemoveItem` on `IInventoryService` for door key consumption — Task 10 Step 2
- [x] `ContainerData.Emptied` flag — Task 11 (ContainerController does not persist the flag to the SO at runtime — acceptable for a demo; persistence is in Pendiente of the spec)
