#nullable enable

using UnityEngine;

namespace CrimsonDraft.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class NoteGrid : MonoBehaviour
    {
        [Header("Grid Config")]
        [SerializeField] private int columns = 4;
        [SerializeField] private int rows    = 4;

        private float         cellSize;
        private RectTransform rectTransform = null!;

        public int   Columns  => this.columns;
        public int   Rows     => this.rows;

        // Derived from RectTransform sizeDelta, same as InventoryGrid.
        // Using sizeDelta (not rect.width) so it is valid before the Canvas layout pass.
        public float CellSize
        {
            get
            {
                if (this.cellSize <= 0f)
                {
                    if (this.rectTransform == null)
                        this.rectTransform = GetComponent<RectTransform>();
                    this.cellSize = this.rectTransform.sizeDelta.x / this.columns;
                }
                return this.cellSize;
            }
        }

        void Awake()
        {
            this.rectTransform = GetComponent<RectTransform>();
            this.cellSize      = this.rectTransform.sizeDelta.x / this.columns;
        }

        // Returns the local anchor position (top-left corner) of a cell.
        // Same formula as InventoryGrid.CellToLocal.
        public Vector2 CellToLocal(Vector2Int cell)
        {
            float cs = CellSize;
            float x  =  cell.x * cs - this.columns * cs * 0.5f;
            float y  = -(cell.y * cs) + this.rows   * cs * 0.5f;
            return new Vector2(x, y);
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            var rt = GetComponent<RectTransform>();
            if (rt == null || this.columns <= 0 || this.rows <= 0) return;

            float cs = rt.sizeDelta.x / this.columns;
            if (cs <= 0f) return;

            UnityEditor.Handles.color = new Color(0f, 1f, 0.8f, 0.5f);

            Vector3 origin = rt.TransformPoint(
                new Vector3(-this.columns * cs * 0.5f, this.rows * cs * 0.5f, 0f));

            Vector3 right = rt.TransformDirection(Vector3.right) * cs;
            Vector3 down  = rt.TransformDirection(Vector3.down)  * cs;

            for (int r = 0; r < this.rows; r++)
            {
                for (int c = 0; c < this.columns; c++)
                {
                    Vector3 tl = origin + right * c + down * r;
                    UnityEditor.Handles.DrawLine(tl,        tl + right);
                    UnityEditor.Handles.DrawLine(tl,        tl + down);
                    UnityEditor.Handles.DrawLine(tl + right, tl + right + down);
                    UnityEditor.Handles.DrawLine(tl + down,  tl + right + down);
                }
            }
        }
#endif
    }
}
