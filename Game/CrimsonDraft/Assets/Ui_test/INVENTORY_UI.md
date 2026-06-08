# Inventory UI System — `Art/ui_crt`

### Overview
This branch implements a complete controller-driven inventory UI system inspired by Resident Evil 4's inventory. The system is built entirely on Unity UI (Canvas/RectTransform) with the New Input System and supports keyboard and gamepad input with no mouse dependency.

---

### Architecture

#### Core Grid System
- **`InventoryGrid`** — A grid component that tracks cell occupancy via a 2D `InventoryItem[,]` array. Cell size is derived automatically from the RectTransform size set in the editor. Supports placement validation, item removal, overlap detection and bounds checking.
- **`InventoryGridGroup`** — Container for up to 4 grids arranged horizontally. Exposes grid access by index.
- **`InventoryItem`** — Runtime item component. Holds a reference to its `ItemData` ScriptableObject, grid origin, rotation state (horizontal/vertical toggle), and inspected flag.
- **`ItemData`** — ScriptableObject defining an item: primary name, secondary name, icon, grid size (1×1 to 4×4), description, and combinable flag.

#### Cursor & Navigation
- **`GridCursor`** — The single cursor shared across all grids. Reads input directly via `Keyboard.current` / `Gamepad.current` for movement (arrows / D-Pad / left stick) with RE4-style hold-to-repeat. Handles cross-grid wrap-around navigation in both directions.

#### Item Interaction
- **Pickup & Move** — Press Square/X key to lift an item. The item follows the cursor with green/red tint feedback for valid/invalid placement. Pressing again drops it. Cancel with Circle/V to return to original position.
- **Rotation** — While holding an item, press Cross/C to toggle between horizontal and vertical orientation. The RectTransform rotates -90° with a position offset correction to keep the item anchored at the cursor's top-left cell.
- **Placement rules (priority order):**
  1. Within grid bounds?
  2. Area empty? → place directly
  3. Single item overlapping? → swap if held item fits
  4. Multiple items overlapping? → blocked

#### Context Menu
- **`ItemContextMenu`** — Appears next to the selected item (flips to left side if near screen edge). Contains Use, Inspect, Combine options. Combine is disabled if `ItemData.combinable` is false. Navigation via up/down, confirm with Cross/C, cancel with Circle/V.
- **`MenuOption`** — Individual option with Normal / Selected / Disabled visual states.

#### Inspect Panel
- **`InspectPanel`** — Full-screen modal showing item icon, primary name and description. Opens via the Inspect option. Fully locks grid navigation until closed with Circle/V. Marks the item as inspected, which changes the tooltip to show the primary name instead of the secondary name.

#### Tooltip
- **`ItemTooltip`** — Shows item name when cursor hovers over an item. Auto-resizes horizontally to fit the text. Positions at top-right of the item, flips to top-left if near screen right edge. When context menu opens, moves above the selector.
- Shows **secondary name** until the item has been inspected, then shows **primary name**.

#### Tab System
- **`TabManager`** — Manages 3 tabs (Inventory, Map, Files) switchable with LB/RB (L1/R1) or Q/E. Activates/deactivates the corresponding root GameObjects and a per-tab indicator object. Cancels any active inventory state (held item, open menus) before switching tabs.

#### Audio
- **`InventorySoundManager`** — Singleton AudioSource wrapper with named methods for every interaction: cursor move, cursor on item, item pickup/move/place/invalid/rotate/swap, menu open/navigate/confirm/cancel, inspect open/close, tab switch. All clips are optional — missing clips are silently ignored.

#### Populator
- **`InventoryPopulator`** — Test utility that randomly places a list of `ItemData` assets across available grids at runtime using a configurable placement attempt limit.

---

### Input Map

| Action | Keyboard | Gamepad |
|---|---|---|
| Navigate | Arrow keys | D-Pad / Left Stick |
| Confirm / Open menu | C | Cross (South) |
| Pickup / Place | X | Square (West) |
| Rotate held item | C | Cross (South) |
| Cancel / Back | V | Circle (East) |
| Next tab | E | RB / R1 |
| Prev tab | Q | LB / L1 |
