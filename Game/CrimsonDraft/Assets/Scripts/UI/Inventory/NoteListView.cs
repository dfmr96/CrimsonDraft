#nullable enable

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using CrimsonDraft.Infrastructure.Save.UI;
using CrimsonDraft.Infrastructure.UI;

namespace CrimsonDraft.UI
{
    /// <summary>
    /// The notes of one category as a plain vertical list, like the Load Game slots: one line of text per
    /// note, the selected one scaled up with the shared selection strip under it. Only as many rows as fit
    /// the list's height exist; the list scrolls to keep the selection in view.
    /// </summary>
    public sealed class NoteListView : MonoBehaviour
    {
        [SerializeField] private RectTransform listRoot    = null!;
        [Tooltip("Un TMP_Text apagado que se clona por cada fila (fuente, tamaño y color de las lineas).")]
        [SerializeField] private TMP_Text      rowTemplate = null!;
        [SerializeField] private float         rowHeight   = 16f;
        [Tooltip("Tira de seleccion compartida (la misma del menu). Opcional.")]
        [SerializeField] private SelectionCursor? selector;
        [Tooltip("Notas que todavia no se encontraron.")]
        [SerializeField, Range(0f, 1f)] private float missingAlpha = 0.55f;

        private sealed class Row
        {
            public TMP_Text          Label  = null!;
            public ManualSelectScale Scale  = null!;
            public RectTransform     Anchor = null!;
        }

        private readonly List<Row> rows = new();
        private string[] labels = System.Array.Empty<string>();
        private bool[]   found  = System.Array.Empty<bool>();
        private int      first;     // index shown on the first row
        private int      selected;
        private bool     selectionVisible;

        public int Count => this.labels.Length;

        /// <summary>How many rows fit the list's height.</summary>
        private int Capacity => Mathf.Max(1, Mathf.FloorToInt(this.listRoot.rect.height / this.rowHeight));

        void Awake() => this.rowTemplate.gameObject.SetActive(false);

        /// <summary>Replaces the list; the selection goes back to the top.</summary>
        public void Bind(IReadOnlyList<string> newLabels, IReadOnlyList<bool> newFound)
        {
            this.labels = new string[newLabels.Count];
            this.found  = new bool[newLabels.Count];
            for (int i = 0; i < newLabels.Count; i++)
            {
                this.labels[i] = newLabels[i];
                this.found[i]  = newFound[i];
            }

            this.first    = 0;
            this.selected = 0;
            EnsureRows(Mathf.Min(Capacity, this.labels.Length));
            Refresh(snapSelector: true);
        }

        public void Select(int index)
        {
            if (this.labels.Length == 0) return;
            this.selected = Mathf.Clamp(index, 0, this.labels.Length - 1);

            // Keep the selection inside the visible window of rows.
            int capacity = Mathf.Min(Capacity, this.labels.Length);
            if (this.selected < this.first)                     this.first = this.selected;
            else if (this.selected >= this.first + capacity)    this.first = this.selected - capacity + 1;

            this.selectionVisible = true;
            Refresh(snapSelector: false);
        }

        public void SetSelectionVisible(bool visible)
        {
            this.selectionVisible = visible;
            Refresh(snapSelector: visible);
        }

        // ── Rows ─────────────────────────────────────────────────────────────

        void EnsureRows(int count)
        {
            while (this.rows.Count < count)
            {
                var label = Instantiate(this.rowTemplate, this.listRoot);
                label.gameObject.name = $"Row_{this.rows.Count}";
                label.gameObject.SetActive(true);

                var rt = (RectTransform)label.transform;
                rt.anchorMin        = new Vector2(0f, 1f);
                rt.anchorMax        = new Vector2(1f, 1f);
                rt.pivot            = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -this.rows.Count * this.rowHeight);
                rt.sizeDelta        = new Vector2(0f, this.rowHeight);

                // Where the strip rests: a thin rect just under the row's bottom edge (pivot on its top, so
                // it hangs below the letters instead of covering them). It hangs from the list,
                // not from the row: the strip copies its target's on-screen size, and the selected row is
                // scaled up, which would push the strip past the edges of the list's background.
                var anchorGo = new GameObject($"SelectionAnchor_{this.rows.Count}", typeof(RectTransform));
                var anchor   = (RectTransform)anchorGo.transform;
                anchor.SetParent(this.listRoot, false);
                anchor.anchorMin        = new Vector2(0f, 1f);
                anchor.anchorMax        = new Vector2(1f, 1f);
                anchor.pivot            = new Vector2(0.5f, 1f);
                anchor.anchoredPosition = new Vector2(0f, -(this.rows.Count + 1) * this.rowHeight);
                anchor.sizeDelta        = new Vector2(0f, 2f);

                this.rows.Add(new Row
                {
                    Label  = label,
                    Scale  = label.gameObject.AddComponent<ManualSelectScale>(),
                    Anchor = anchor,
                });
            }
        }

        void Refresh(bool snapSelector)
        {
            int visible = Mathf.Min(this.rows.Count, this.labels.Length);
            for (int r = 0; r < this.rows.Count; r++)
            {
                var row = this.rows[r];
                int i   = this.first + r;
                bool used = r < visible && i < this.labels.Length;
                row.Label.gameObject.SetActive(used);
                if (!used) continue;

                row.Label.text  = this.labels[i];
                var color       = row.Label.color;
                color.a         = this.found[i] ? 1f : this.missingAlpha;
                row.Label.color = color;
                row.Scale.SetSelected(this.selectionVisible && i == this.selected);
            }

            if (this.selector == null) return;
            int selectedRow = this.selected - this.first;
            bool show = this.selectionVisible && selectedRow >= 0 && selectedRow < visible;
            this.selector.FollowOrHide(show ? this.rows[selectedRow].Anchor : null, snapSelector);
        }
    }
}
