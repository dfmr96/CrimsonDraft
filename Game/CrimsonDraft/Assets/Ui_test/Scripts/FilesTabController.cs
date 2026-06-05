#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.UI
{
    public sealed class FilesTabController : MonoBehaviour
    {
        [SerializeField] private NoteDatabase      database      = null!;
        [SerializeField] private NoteDetailView    detailView    = null!;
        [SerializeField] private NoteGridCellView  cellPrefab    = null!;
        [SerializeField] private NoteGrid          noteGrid      = null!;
        [SerializeField] private RectTransform     selectorRect  = null!;
        [SerializeField] private TMP_Text          categoryLabel = null!;
        [SerializeField] private TMP_Text?         previewLabel;
        [SerializeField] private GameObject?       prevArrow;
        [SerializeField] private GameObject?       nextArrow;

        [Header("Highlights")]
        [SerializeField] private GameObject? carouselHighlight;
        [SerializeField] private GameObject? prevArrowHighlight;
        [SerializeField] private GameObject? nextArrowHighlight;
        [SerializeField] private float       arrowFlashTime = 0.12f;

        [Header("Navigation Feel")]
        [SerializeField] private float initialRepeatDelay = 0.4f;
        [SerializeField] private float repeatInterval     = 0.12f;

        [Inject] private IInputService inputService = null!;
        [Inject] private TabManager    tabManager   = null!;

        private static readonly DocumentCategory[] Categories =
            (DocumentCategory[])Enum.GetValues(typeof(DocumentCategory));

        private int categoryIndex;
        private int selectedIndex;

        private readonly List<NoteGridCellView> cells         = new();
        private          DocumentData[]         filteredNotes = Array.Empty<DocumentData>();

        private bool       holding;
        private Vector2Int lastDir;
        private float      nextMoveTime;
        private bool       wasTabBarActive;

        private enum Focus { Carousel, Grid }
        private Focus focus;

        // ── Lifecycle ────────────────────────────────────────────────────────

        void Start()
        {
            if (this.selectorRect == null || this.noteGrid == null) return;
            this.selectorRect.SetParent(this.noteGrid.transform, false);
            this.selectorRect.anchorMin = new Vector2(0.5f, 0.5f);
            this.selectorRect.anchorMax = new Vector2(0.5f, 0.5f);
            this.selectorRect.pivot     = new Vector2(0f, 1f);
        }

        void OnEnable()
        {
            if (this.inputService == null) return;
            this.inputService.InventoryConfirm.performed += OnConfirm;
            this.inputService.InventoryCancel.performed  += OnCancel;

            this.categoryIndex   = 0;
            this.selectedIndex   = 0;
            this.holding         = false;
            this.lastDir         = Vector2Int.zero;
            this.wasTabBarActive = false;

            this.selectorRect.gameObject.SetActive(false);
            SetFocus(Focus.Carousel);

            if (this.database != null)
                RefreshCategory();
        }

        void OnDisable()
        {
            if (this.inputService == null) return;
            this.inputService.InventoryConfirm.performed -= OnConfirm;
            this.inputService.InventoryCancel.performed  -= OnCancel;
        }

        void Update()
        {
            if (this.tabManager == null) return;
            bool tabBarNow = this.tabManager.IsTabBarActive;
            if (tabBarNow != this.wasTabBarActive)
            {
                if (!tabBarNow)
                {
                    SetFocus(Focus.Carousel);
                    this.selectorRect.gameObject.SetActive(false);
                }
                this.wasTabBarActive = tabBarNow;
            }
            if (tabBarNow || this.detailView.IsOpen) return;

            Vector2Int dir = ReadDirection();

            if (dir == Vector2Int.zero)
            {
                this.holding = false;
                this.lastDir = Vector2Int.zero;
                return;
            }

            if (dir != this.lastDir)
            {
                ProcessMove(dir);
                this.lastDir      = dir;
                this.holding      = true;
                this.nextMoveTime = Time.unscaledTime + this.initialRepeatDelay;
            }
            else if (this.holding && Time.unscaledTime >= this.nextMoveTime)
            {
                ProcessMove(dir);
                this.nextMoveTime = Time.unscaledTime + this.repeatInterval;
            }
        }

        // ── Navigation ───────────────────────────────────────────────────────

        void ProcessMove(Vector2Int dir)
        {
            if (this.focus == Focus.Carousel)
                MoveCarousel(dir);
            else
                MoveGrid(dir);
        }

        void MoveCarousel(Vector2Int dir)
        {
            if (dir.y < 0)
            {
                if (this.filteredNotes.Length > 0)
                {
                    SetFocus(Focus.Grid);
                    this.selectedIndex = 0;
                    PlaceSelectorAt(this.selectedIndex);
                }
                return;
            }

            // LEFT / RIGHT — change category + flash the pressed arrow
            this.categoryIndex = (this.categoryIndex + dir.x + Categories.Length) % Categories.Length;
            FlashArrowHighlight(dir.x < 0 ? this.prevArrowHighlight : this.nextArrowHighlight);
            RefreshCategory();
        }

        void MoveGrid(Vector2Int dir)
        {
            int col = this.selectedIndex % this.noteGrid.Columns;
            int row = this.selectedIndex / this.noteGrid.Columns;

            if (dir.y > 0)
            {
                if (row == 0)
                {
                    SetFocus(Focus.Carousel);
                    this.selectorRect.gameObject.SetActive(false);
                }
                else
                {
                    this.selectedIndex -= this.noteGrid.Columns;
                    PlaceSelectorAt(this.selectedIndex);
                }
                return;
            }

            if (dir.y < 0)
            {
                int next = this.selectedIndex + this.noteGrid.Columns;
                if (next < this.filteredNotes.Length)
                {
                    this.selectedIndex = next;
                    PlaceSelectorAt(this.selectedIndex);
                }
                return;
            }

            if (dir.x > 0 && col < this.noteGrid.Columns - 1)
            {
                int next = this.selectedIndex + 1;
                if (next < this.filteredNotes.Length)
                {
                    this.selectedIndex = next;
                    PlaceSelectorAt(this.selectedIndex);
                }
                return;
            }

            if (dir.x < 0 && col > 0)
            {
                this.selectedIndex--;
                PlaceSelectorAt(this.selectedIndex);
            }
        }

        // ── Input callbacks ──────────────────────────────────────────────────

        void OnConfirm(InputAction.CallbackContext _)
        {
            if (this.tabManager.IsTabBarActive) return;
            if (this.detailView.IsOpen)         return;
            if (this.focus != Focus.Grid)       return;
            if (this.filteredNotes.Length == 0) return;

            var doc  = this.filteredNotes[this.selectedIndex];
            var body = doc.Pages.Length > 0 ? string.Join("\n\n", doc.Pages) : string.Empty;
            this.detailView.Show(doc.Title, body);
        }

        void OnCancel(InputAction.CallbackContext _)
        {
            if (this.detailView.IsOpen)
            {
                this.detailView.Hide();
                return;
            }
            if (!this.tabManager.IsTabBarActive)
                this.tabManager.EnterTabBar();
        }

        // ── Focus / Highlights ────────────────────────────────────────────────

        void SetFocus(Focus newFocus)
        {
            this.focus = newFocus;
            this.carouselHighlight?.SetActive(newFocus == Focus.Carousel);
            if (newFocus == Focus.Carousel && this.previewLabel != null)
                this.previewLabel.text = string.Empty;
        }

        void FlashArrowHighlight(GameObject? highlight)
        {
            if (highlight == null) return;
            StopCoroutine(nameof(FlashHighlightRoutine));
            StartCoroutine(FlashHighlightRoutine(highlight));
        }

        IEnumerator FlashHighlightRoutine(GameObject highlight)
        {
            highlight.SetActive(true);
            yield return new WaitForSecondsRealtime(this.arrowFlashTime);
            if (highlight != null) highlight.SetActive(false);
        }

        // ── Category carousel ────────────────────────────────────────────────

        void RefreshCategory()
        {
            var cat = Categories[this.categoryIndex];
            this.categoryLabel.text = DisplayName(cat);
            this.prevArrow?.SetActive(true);
            this.nextArrow?.SetActive(true);

            this.filteredNotes = this.database.Notes
                .Where(n => n != null && n.Category == cat)
                .ToArray();

            RebuildGrid();

            this.selectedIndex = 0;
            if (this.focus == Focus.Grid && this.filteredNotes.Length > 0)
                PlaceSelectorAt(this.selectedIndex);
            else
                this.selectorRect.gameObject.SetActive(false);
        }

        static string DisplayName(DocumentCategory cat) => cat switch
        {
            DocumentCategory.DeckB     => "Deck B",
            DocumentCategory.DeckC     => "Deck C",
            DocumentCategory.ElChef    => "El Chef",
            DocumentCategory.Ingeniero => "Ingeniero",
            _                          => cat.ToString()
        };

        // ── Grid ─────────────────────────────────────────────────────────────

        void RebuildGrid()
        {
            foreach (var c in this.cells)
                if (c != null) Destroy(c.gameObject);
            this.cells.Clear();

            for (int i = 0; i < this.filteredNotes.Length; i++)
            {
                var cell = new Vector2Int(i % this.noteGrid.Columns, i / this.noteGrid.Columns);

                var go   = Instantiate(this.cellPrefab, this.noteGrid.transform);
                var view = go.GetComponent<NoteGridCellView>();
                var rt   = go.GetComponent<RectTransform>();

                rt.anchorMin        = new Vector2(0.5f, 0.5f);
                rt.anchorMax        = new Vector2(0.5f, 0.5f);
                rt.pivot            = new Vector2(0f, 1f);
                rt.anchoredPosition = this.noteGrid.CellToLocal(cell);
                rt.sizeDelta        = new Vector2(this.noteGrid.CellSize, this.noteGrid.CellSize);

                view.Bind(this.filteredNotes[i]);
                this.cells.Add(view);
            }
        }

        // ── Selector ─────────────────────────────────────────────────────────

        void PlaceSelectorAt(int index)
        {
            if (index < 0 || index >= this.cells.Count)
            {
                this.selectorRect.gameObject.SetActive(false);
                return;
            }

            var cell = new Vector2Int(index % this.noteGrid.Columns, index / this.noteGrid.Columns);

            this.selectorRect.gameObject.SetActive(true);
            this.selectorRect.anchoredPosition = this.noteGrid.CellToLocal(cell);
            this.selectorRect.sizeDelta        = new Vector2(this.noteGrid.CellSize, this.noteGrid.CellSize);
            this.selectorRect.SetAsLastSibling();

            if (this.previewLabel != null)
                this.previewLabel.text = this.filteredNotes[index].Title;
        }

        // ── Tab bar integration ───────────────────────────────────────────────

        public void HideSelectorForTabBar()
        {
            this.selectorRect.gameObject.SetActive(false);
            this.holding = false;
            this.lastDir = Vector2Int.zero;
        }

        public void ShowSelectorAfterTabBar()
        {
            SetFocus(Focus.Carousel);
            this.selectorRect.gameObject.SetActive(false);
        }

        // ── Input reading ────────────────────────────────────────────────────

        Vector2Int ReadDirection()
        {
            Vector2 raw = this.inputService.InventoryNavigate.ReadValue<Vector2>();
            if (raw.sqrMagnitude < 0.01f) return Vector2Int.zero;
            float ax = Mathf.Abs(raw.x);
            float ay = Mathf.Abs(raw.y);
            if (ax >= ay) return raw.x > 0 ? Vector2Int.right : Vector2Int.left;
            return raw.y > 0 ? Vector2Int.up : Vector2Int.down;
        }
    }
}
