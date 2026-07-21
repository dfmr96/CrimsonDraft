#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MessagePipe;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Infrastructure.Input;
using Yarn.Unity;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.UI
{
    public sealed class FilesTabController : MonoBehaviour, IInitializable, IDisposable
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

        [SerializeField] private YarnProject yarnProject = null!;

        [Inject] private IInputService                    inputService         = null!;
        [Inject] private TabManager                       tabManager           = null!;
        [Inject] private InventoryOpenCloseController     openCloseController  = null!;
        [Inject] private ISubscriber<NoteCollectedEvent>   noteCollectedSub     = null!;
        [Inject] private InventorySfxData                 sfx                  = null!;
        [Inject] private NoteRegistry                      noteRegistry         = null!;

        private IDisposable? noteSubscription;

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

        private string[] currentPages = Array.Empty<string>();
        private Sprite?  currentImage;
        private int      pageIndex;
        private string   currentTitle = string.Empty;

        // Image + title-only pages come before the text pages when the doc has a PageImage.
        private int TotalPages => this.currentPages.Length + (this.currentImage != null ? 2 : 0);

        private enum Focus { Carousel, Grid }
        private Focus focus;

        // ── DI lifecycle ─────────────────────────────────────────────────────

        void IInitializable.Initialize()
        {
            this.noteSubscription = this.noteCollectedSub.Subscribe(OnNoteCollected);
        }

        void IDisposable.Dispose()
        {
            this.noteSubscription?.Dispose();
        }

        void OnNoteCollected(NoteCollectedEvent e)
        {
            var doc = this.database.Notes.FirstOrDefault(n => n != null && n.NoteId == e.NoteId);
            if (doc == null) return;

            // Open the canvas first so the whole hierarchy (GridCursor, etc.) gets its Awake()
            // call — TabManager.EnsureInitialized() below ends up calling ResetForOpen(), which
            // touches GridCursor.CurrentGrid and NREs if GridCursor hasn't awoken yet.
            this.openCloseController.Open();

            // Then force TabManager's first-time setup synchronously, in case Unity hasn't
            // gotten around to calling its Start() yet — otherwise the deferred Start() would
            // reset the active tab back to "Inventory" right after we switch to "Files".
            this.tabManager.EnsureInitialized();
            this.tabManager.ActivateTabByName("Files");
            OpenCollected(doc);
        }

        /// <summary>Opens a note exactly as if the player had navigated to it by hand.</summary>
        public void OpenCollected(DocumentData doc)
        {
            EnsureSelectorParented();

            int catIndex = Array.IndexOf(Categories, doc.Category);
            this.categoryIndex = catIndex >= 0 ? catIndex : 0;
            RefreshCategory();

            int idx = Array.IndexOf(this.filteredNotes, doc);
            if (idx >= 0)
            {
                this.selectedIndex = idx;
                SetFocus(Focus.Grid);
                PlaceSelectorAt(this.selectedIndex);
            }

            OpenNote(doc);
        }

        // ── Lifecycle ────────────────────────────────────────────────────────

        void Start() => EnsureSelectorParented();

        private bool selectorParented;

        void EnsureSelectorParented()
        {
            if (this.selectorParented) return;
            this.selectorParented = true;

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

            this.sfx?.PlayCursor(gameObject);
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

            if (this.detailView.IsOpen)
            {
                AdvancePage();
                return;
            }

            if (this.focus != Focus.Grid)       return;
            if (this.filteredNotes.Length == 0) return;

            var doc = this.filteredNotes[this.selectedIndex];
            if (!string.IsNullOrEmpty(doc.NoteId))
            {
                this.sfx?.PlayFileOpen(gameObject);
                OpenNote(doc);
            }
        }

        void OnCancel(InputAction.CallbackContext _)
        {
            if (this.detailView.IsOpen)
            {
                this.sfx?.PlayCancel(gameObject);
                this.detailView.Hide();
                return;
            }
            if (!this.tabManager.IsTabBarActive)
                this.tabManager.EnterTabBar();
        }

        void OpenNote(DocumentData doc)
        {
            if (this.yarnProject == null) return;

            const string PageBreak = "<page>";

            var lineIds      = this.yarnProject.GetLineIDsForNodes(new[] { doc.NoteId });
            var localization = this.yarnProject.baseLocalization;
            var pages        = new List<string>();
            var current      = new System.Text.StringBuilder();

            foreach (var id in lineIds)
            {
                var text = localization.GetLocalizedString(id);
                if (text == null) continue;

                if (text == PageBreak)
                {
                    if (current.Length > 0)
                    {
                        pages.Add(current.ToString());
                        current.Clear();
                    }
                }
                else
                {
                    if (current.Length > 0) current.Append('\n');
                    current.Append(text);
                }
            }

            if (current.Length > 0)
                pages.Add(current.ToString());

            this.currentImage = doc.PageImage;
            if (pages.Count == 0 && this.currentImage == null) return;

            this.currentPages = pages.ToArray();
            this.pageIndex    = 0;
            this.currentTitle = doc.Title;
            ShowCurrentPage();
        }

        void ShowCurrentPage()
        {
            int total = TotalPages;

            if (this.currentImage != null)
            {
                if (this.pageIndex == 0)
                {
                    this.detailView.ShowImage(this.currentImage, this.pageIndex + 1, total);
                    return;
                }
                if (this.pageIndex == 1)
                {
                    this.detailView.ShowTitleOnly(this.currentTitle, this.pageIndex + 1, total);
                    return;
                }

                this.detailView.ShowBodyOnly(this.currentPages[this.pageIndex - 2], this.pageIndex + 1, total);
                return;
            }

            this.detailView.Show(this.currentTitle, this.currentPages[this.pageIndex], this.pageIndex + 1, total);
        }

        void AdvancePage()
        {
            this.pageIndex++;
            if (this.pageIndex >= TotalPages)
            {
                this.detailView.Hide();
                return;
            }
            ShowCurrentPage();
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
                .Where(n => n != null && n.Category == cat && this.noteRegistry.IsCollected(n.NoteId))
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
            DocumentCategory.Notes      => "Notas",
            DocumentCategory.VoiceNotes => "Notas de Voz",
            DocumentCategory.Posters    => "Carteles",
            _                           => cat.ToString()
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
