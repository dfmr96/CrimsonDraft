#nullable enable

#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections.Generic;
using System.Text;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Navigation
{
    /// <summary>
    /// Live inventory debug overlay. Because InventoryService is a shared singleton it
    /// reflects real state during both navigation and combat. Toggle the on-screen panel
    /// with <see cref="toggleKey"/> (default F1) in Play Mode, or press the inspector
    /// button to dump a snapshot to the Console.
    ///
    /// Color code per slot:
    ///   white  = positioned item (GridCol/Row persisted)
    ///   yellow = unpositioned item (GridCol=-1, relies on runtime auto-placement)
    ///   red    = two positioned items claim the same cell (placement collision)
    /// </summary>
    public sealed class InventoryDebugPrinter : MonoBehaviour
    {
        [SerializeField] private Key  toggleKey   = Key.F1;
        [SerializeField] private bool showOnStart = false;

        private IInventoryService? inventory;
        private IOperatorRoster?   roster;

        private bool      visible;
        private Vector2   scroll;
        private GUIStyle? headerStyle;
        private GUIStyle? slotStyle;

        [Inject]
        public void Construct(IInventoryService inventory, IOperatorRoster roster)
        {
            this.inventory = inventory;
            this.roster    = roster;
        }

        private void Start() => this.visible = this.showOnStart;

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb[this.toggleKey].wasPressedThisFrame)
                this.visible = !this.visible;
        }

        // ── Inspector snapshot ───────────────────────────────────────────────

        [Button("Print Inventory To Console")]
        public void PrintInventory()
        {
            if (this.inventory == null || this.roster == null)
            {
                Debug.LogWarning("[InventoryDebug] Not injected yet — enter Play Mode first.");
                return;
            }
            Debug.Log(BuildDump());
        }

        // ── Live overlay ─────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!this.visible || this.inventory == null || this.roster == null) return;

            EnsureStyles();

            const float width = 360f;
            float height = Mathf.Min(Screen.height - 20f, 40f + this.roster.Count * 130f);

            GUILayout.BeginArea(new Rect(10f, 10f, width, height), GUI.skin.box);
            GUILayout.Label($"INVENTORY  ({this.inventory.SlotCount} slots)   [{this.toggleKey}] to hide",
                this.headerStyle);

            this.scroll = GUILayout.BeginScrollView(this.scroll);

            int slotsPerOp = this.roster.Count > 0
                ? this.inventory.SlotCount / this.roster.Count
                : 4;

            for (int op = 0; op < this.roster.Count; op++)
            {
                string opName = this.roster[op].Data?.DisplayName ?? $"Operator {op}";
                GUILayout.Space(4f);
                GUILayout.Label($"OP {op} — {opName}", this.headerStyle);

                int start = op * slotsPerOp;
                int end   = Mathf.Min(start + slotsPerOp, this.inventory.SlotCount);

                HashSet<Vector2Int> occupied = CollectPositionedCells(start, end);

                for (int i = start; i < end; i++)
                {
                    InventorySlot slot = this.inventory.Slots[i];
                    DrawSlotRow(i, slot, occupied);
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawSlotRow(int index, InventorySlot slot, HashSet<Vector2Int> collisionCells)
        {
            if (slot.IsEmpty || slot.Item == null)
            {
                GUI.color = new Color(0.6f, 0.6f, 0.6f);
                GUILayout.Label($"  slot[{index}]  <empty>", this.slotStyle);
                GUI.color = Color.white;
                return;
            }

            ItemData data     = slot.Item.Data;
            bool     placed   = slot.GridCol >= 0 && slot.GridRow >= 0;
            bool     collides = placed && CellsOverlap(slot, collisionCells);

            GUI.color = collides ? new Color(1f, 0.4f, 0.4f)
                      : placed   ? Color.white
                                 : new Color(1f, 0.85f, 0.3f);

            string pos = placed ? $"({slot.GridCol},{slot.GridRow})" : "UNPLACED";
            string tag = collides ? "  <COLLISION>" : "";
            GUILayout.Label(
                $"  slot[{index}]  {data.DisplayName} x{slot.Quantity}  " +
                $"{data.GridSize.x}x{data.GridSize.y}  {pos} rot{slot.GridRotation}{tag}",
                this.slotStyle);

            GUI.color = Color.white;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private HashSet<Vector2Int> CollectPositionedCells(int start, int end)
        {
            // Cells claimed by MORE THAN ONE positioned item = collision cells.
            var seen      = new HashSet<Vector2Int>();
            var collision = new HashSet<Vector2Int>();

            for (int i = start; i < end; i++)
            {
                InventorySlot slot = this.inventory!.Slots[i];
                if (slot.IsEmpty || slot.Item == null) continue;
                if (slot.GridCol < 0 || slot.GridRow < 0) continue;

                foreach (Vector2Int cell in CellsOf(slot))
                    if (!seen.Add(cell))
                        collision.Add(cell);
            }
            return collision;
        }

        private static bool CellsOverlap(InventorySlot slot, HashSet<Vector2Int> collisionCells)
        {
            foreach (Vector2Int cell in CellsOf(slot))
                if (collisionCells.Contains(cell))
                    return true;
            return false;
        }

        private static IEnumerable<Vector2Int> CellsOf(InventorySlot slot)
        {
            Vector2Int size = slot.GridRotation == 0
                ? slot.Item!.Data.GridSize
                : new Vector2Int(slot.Item!.Data.GridSize.y, slot.Item.Data.GridSize.x);

            for (int c = 0; c < size.x; c++)
                for (int r = 0; r < size.y; r++)
                    yield return new Vector2Int(slot.GridCol + c, slot.GridRow + r);
        }

        private string BuildDump()
        {
            int slotsPerOp = this.roster!.Count > 0
                ? this.inventory!.SlotCount / this.roster.Count
                : 4;

            var sb = new StringBuilder();
            sb.AppendLine($"===== INVENTORY DUMP ({this.inventory!.SlotCount} slots, {this.roster.Count} ops) =====");
            for (int op = 0; op < this.roster.Count; op++)
            {
                string opName = this.roster[op].Data?.DisplayName ?? $"Operator {op}";
                sb.AppendLine($"[OP {op}] {opName}");

                int start = op * slotsPerOp;
                int end   = Mathf.Min(start + slotsPerOp, this.inventory.SlotCount);
                for (int i = start; i < end; i++)
                {
                    InventorySlot slot = this.inventory.Slots[i];
                    if (slot.IsEmpty || slot.Item == null) { sb.AppendLine($"    slot[{i}] <empty>"); continue; }

                    ItemData data = slot.Item.Data;
                    string pos = slot.GridCol >= 0 ? $"({slot.GridCol},{slot.GridRow})" : "UNPLACED";
                    sb.AppendLine($"    slot[{i}] {data.DisplayName} x{slot.Quantity} " +
                                  $"type={data.ItemType} size={data.GridSize} grid={pos} rot={slot.GridRotation}");
                }
            }
            return sb.ToString();
        }

        private void EnsureStyles()
        {
            this.headerStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize  = 12,
            };
            this.slotStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize  = 11,
                richText  = true,
                wordWrap  = false,
            };
        }
    }
}

#endif
