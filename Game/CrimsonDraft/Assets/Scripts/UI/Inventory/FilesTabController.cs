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

        [Header("Voice Note Playback")]
        [SerializeField] private float voiceCharsPerSecond  = 18f;
        [SerializeField] private float voiceScrubMultiplier = 4f;
        [Tooltip("Minimum real-time gap between typewriter ticks -- caps the tick rate independently of reveal speed, so holding right to scrub (voiceScrubMultiplier) never turns it into a machine-gun.")]
        [SerializeField] private float voiceTypewriterMinInterval = 0.35f;

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

        // Yarn's own grammar rejects a bare '<' in line text, so TMP rich-text tags can't be
        // authored directly in .yarn source. Writers instead wrap damaged/stained characters
        // in tildes (e.g. "yo~ur~ S~up~erior") and this turns each ~run~ into a solid white
        // bar (mark + matching text color, so the letters underneath disappear into it) once
        // the line has already come back out of Yarn as plain text. White matches the body
        // text color itself, not the panel background.
        private static readonly System.Text.RegularExpressions.Regex DamagedTextPattern =
            new(@"~(.+?)~", System.Text.RegularExpressions.RegexOptions.Compiled);

        const string DamagedTextColor = "#FFFFFF";

        static string ApplyDamagedTextMarkup(string text) =>
            DamagedTextPattern.Replace(text, $"<mark={DamagedTextColor}FF><color={DamagedTextColor}>$1</color></mark>");

        // Same problem, same fix, for italics: writers wrap a run in *asterisks* and it comes
        // back out as <i>...</i> once the line is plain text again.
        private static readonly System.Text.RegularExpressions.Regex ItalicTextPattern =
            new(@"\*(.+?)\*", System.Text.RegularExpressions.RegexOptions.Compiled);

        static string ApplyItalicMarkup(string text) =>
            ItalicTextPattern.Replace(text, "<i>$1</i>");

        static string ApplyTextMarkup(string text) =>
            ApplyItalicMarkup(ApplyDamagedTextMarkup(text));

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

        private bool    isVoiceNote;
        private bool    inVoiceBody;
        private int     voiceParagraphIndex;
        private float   voiceCharsShown;
        private float   voiceTotalDuration;
        private float[] voiceParagraphDurations = Array.Empty<float>();
        private int     voiceScrubDirection;
        private bool    voicePaused;
        private int     voiceLastDirX;
        private int     voiceLastTypedChars; // last "shown" count a typewriter tick was played for
        private float   voiceLastTypewriterTime = -999f; // Time.unscaledTime of the last tick

        private static readonly System.Text.RegularExpressions.Regex RichTagPattern =
            new(@"<[^>]+>", System.Text.RegularExpressions.RegexOptions.Compiled);

        // maxVisibleCharacters counts glyphs, not markup, so duration/reveal math has to strip
        // tags the same way TMP does or a heavily-tagged paragraph would look like it "types"
        // faster than an untagged one of the same on-screen length.
        static int CountVisibleChars(string markupText) => RichTagPattern.Replace(markupText, string.Empty).Length;

        static string FormatVoiceClock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{s / 60:00}:{s % 60:00}";
        }

        // direction: -1 while rewinding, +1 while fast-forwarding, 0 otherwise -- the held arrow
        // is drawn larger so it reads as a pressed button.
        static string FormatVoiceTimer(float remainingSeconds, float totalSeconds, int direction)
        {
            string time = $"{FormatVoiceClock(remainingSeconds)}/{FormatVoiceClock(totalSeconds)}";

            string rewind  = direction < 0 ? "<size=150%><<</size>" : "<<";
            string forward = direction > 0 ? "<size=150%>>></size>" : ">>";

            return $"{rewind}  {time}  {forward}";
        }

        // Image + title-only pages come before the text pages when the doc has a PageImage.
        // A voice note collapses every paragraph into one continuous playback phase instead of
        // a discrete page per paragraph — see EnterVoiceBody / UpdateVoicePlayback.
        private int NonBodyPageCount => this.currentImage != null ? 2 : 0;

        private int TotalPages =>
            this.isVoiceNote
                ? this.NonBodyPageCount + 1
                : this.currentPages.Length + (this.currentImage != null ? 2 : 0);

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
            if (tabBarNow) return;

            if (this.detailView.IsOpen)
            {
                if (this.inVoiceBody) UpdateVoicePlayback();
                else                  UpdateDetailPaging();
                return;
            }

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

        // Left/Right page a note the same as Confirm/Cancel while it's open; Up/Down are ignored
        // here since they don't mean anything for a single column of reading pages.
        void UpdateDetailPaging()
        {
            Vector2Int dir = ReadDirection();

            if (dir == Vector2Int.zero || dir.x == 0)
            {
                this.holding = false;
                this.lastDir = Vector2Int.zero;
                return;
            }

            if (dir != this.lastDir)
            {
                if (dir.x > 0) AdvancePage(); else PreviousPage();
                this.lastDir      = dir;
                this.holding      = true;
                this.nextMoveTime = Time.unscaledTime + this.initialRepeatDelay;
            }
            else if (this.holding && Time.unscaledTime >= this.nextMoveTime)
            {
                if (dir.x > 0) AdvancePage(); else PreviousPage();
                this.nextMoveTime = Time.unscaledTime + this.repeatInterval;
            }
        }

        // ── Voice note playback ─────────────────────────────────────────────────

        void EnterVoiceBody()
        {
            this.inVoiceBody         = true;
            this.voiceParagraphIndex = 0;
            this.voiceCharsShown     = 0f;
            this.voiceScrubDirection = 0;
            this.voicePaused         = false;
            this.voiceLastDirX       = 0;
            this.voiceLastTypedChars = 0;
            this.voiceLastTypewriterTime = -999f;
            this.sfx?.PlayVoiceStart(gameObject);
            ShowVoiceParagraph();
        }

        // Runs every frame while a voice note's transcript is on screen. Each paragraph is its
        // own page: crossing into the next (or previous) one always starts it from a blank body
        // label, so the outgoing paragraph's text never lingers on screen. Playback advances on
        // its own within a paragraph -- simulating the recording running -- and holding
        // right/left scrubs the reveal forward/backward. Moving into the NEXT paragraph always
        // needs its own fresh press of right -- holding it through the boundary just parks at the
        // end of the current one instead of sweeping through several paragraphs unattended.
        // Rewinding back into a previous paragraph stays continuous, since that one's meant to
        // feel like rewinding a tape rather than turning a page.
        void UpdateVoicePlayback()
        {
            if (this.voicePaused) return;

            Vector2Int dir = ReadDirection();

            bool freshRight = dir.x > 0 && this.voiceLastDirX <= 0;
            this.voiceLastDirX = dir.x;

            float rate = 1f;
            this.voiceScrubDirection = 0;
            if (dir.x > 0) { rate = this.voiceScrubMultiplier;  this.voiceScrubDirection = 1; }
            else if (dir.x < 0) { rate = -this.voiceScrubMultiplier; this.voiceScrubDirection = -1; }

            this.voiceCharsShown += rate * this.voiceCharsPerSecond * Time.unscaledDeltaTime;

            int totalVisible = CountVisibleChars(this.currentPages[this.voiceParagraphIndex]);

            if (this.voiceCharsShown > totalVisible)
            {
                bool hasNext = this.voiceParagraphIndex < this.currentPages.Length - 1;

                if (hasNext && freshRight)
                {
                    // Carry the overshoot into the next paragraph instead of snapping to 0, so a
                    // fast scrub doesn't lose momentum at the boundary.
                    float overflow = this.voiceCharsShown - totalVisible;
                    this.voiceParagraphIndex++;
                    this.voiceCharsShown = overflow;
                    this.voiceLastTypedChars = Mathf.FloorToInt(overflow);
                }
                else
                {
                    // Paragraph finished -- hold here until a fresh right press moves us on.
                    this.voiceCharsShown = totalVisible;
                }
            }
            else if (this.voiceCharsShown < 0f)
            {
                if (this.voiceParagraphIndex > 0)
                {
                    float overflow = this.voiceCharsShown;
                    this.voiceParagraphIndex--;
                    this.voiceCharsShown = CountVisibleChars(this.currentPages[this.voiceParagraphIndex]) + overflow;
                    this.voiceLastTypedChars = Mathf.FloorToInt(this.voiceCharsShown);
                }
                else
                {
                    // Rewound past the start of the transcript -- fall back into the previous
                    // discrete page (the title screen, or close the note if there wasn't one).
                    this.voiceCharsShown = 0f;
                    this.inVoiceBody     = false;
                    this.pageIndex--;
                    if (this.pageIndex < 0)
                    {
                        this.detailView.Hide();
                        return;
                    }
                    ShowCurrentPage();
                    return;
                }
            }

            ShowVoiceParagraph();
        }

        void ShowVoiceParagraph()
        {
            string paragraph    = this.currentPages[this.voiceParagraphIndex];
            int    totalVisible = CountVisibleChars(paragraph);
            int    shown        = Mathf.Clamp(Mathf.FloorToInt(this.voiceCharsShown), 0, totalVisible);

            // Typewriter tick: only past the furthest point ever revealed in this paragraph
            // (voiceLastTypedChars only ever grows here, via Mathf.Max) -- otherwise rewinding
            // and then scrubbing forward again over text that's already fully on screen would
            // replay ticks for characters that aren't actually being "typed" anymore. Also
            // gated by a minimum real-time interval rather than a character count -- a
            // character-count gate still machine-guns while scrubbing (voiceScrubMultiplier
            // reveals characters up to 4x faster), since more real ticks fit in the same
            // second. A wall-clock cooldown keeps the tick rate constant regardless of speed.
            if (shown > this.voiceLastTypedChars &&
                Time.unscaledTime - this.voiceLastTypewriterTime >= this.voiceTypewriterMinInterval)
            {
                this.sfx?.PlayVoiceTypewriter(gameObject);
                this.voiceLastTypewriterTime = Time.unscaledTime;
            }
            this.voiceLastTypedChars = Mathf.Max(this.voiceLastTypedChars, shown);

            float elapsedBefore = 0f;
            for (int i = 0; i < this.voiceParagraphIndex; i++)
                elapsedBefore += this.voiceParagraphDurations[i];

            float remaining = this.voiceTotalDuration - elapsedBefore - (this.voiceCharsShown / this.voiceCharsPerSecond);
            remaining = Mathf.Clamp(remaining, 0f, this.voiceTotalDuration);

            string timerText       = FormatVoiceTimer(remaining, this.voiceTotalDuration, this.voiceScrubDirection);
            bool   showTitleInline = this.currentImage == null;

            this.detailView.ShowVoiceBody(this.currentTitle, paragraph, shown, timerText, showTitleInline, this.currentImage);
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
                if (this.inVoiceBody)
                {
                    // Confirm (A / its keyboard equivalent) toggles pause on the recording
                    // instead of turning a page -- press again to resume where it left off.
                    this.voicePaused = !this.voicePaused;
                    return;
                }

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

        // Called by TabManager.OnCancelTab — the sole subscriber to InventoryCancel — so the
        // open note detail view gets first chance to consume Cancel before TabManager falls
        // back to entering the tab bar selector.
        public bool TryConsumeCancel()
        {
            if (!this.detailView.IsOpen) return false;

            this.sfx?.PlayCancel(gameObject);
            this.detailView.Hide();
            return true;
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
                    current.Append(ApplyTextMarkup(text));
                }
            }

            if (current.Length > 0)
                pages.Add(current.ToString());

            this.currentImage = doc.PageImage;
            if (pages.Count == 0 && this.currentImage == null) return;

            this.currentPages = pages.ToArray();
            this.pageIndex    = 0;
            this.currentTitle = doc.Title;

            // Voice notes still need a transcript to type out -- an image-only voice doc (none
            // exist yet, but nothing stops one) just falls back to the normal instant reveal.
            this.isVoiceNote = doc.Category == DocumentCategory.VoiceNotes && pages.Count > 0;
            this.inVoiceBody = false;
            if (this.isVoiceNote)
            {
                this.voiceParagraphDurations = this.currentPages
                    .Select(p => Mathf.Max(0.1f, CountVisibleChars(p) / this.voiceCharsPerSecond))
                    .ToArray();
                this.voiceTotalDuration = this.voiceParagraphDurations.Sum();
            }

            ShowCurrentPage();
        }

        void ShowCurrentPage()
        {
            if (this.isVoiceNote && this.pageIndex >= this.NonBodyPageCount)
            {
                EnterVoiceBody();
                return;
            }

            int total = TotalPages;

            if (this.currentImage != null)
            {
                if (this.pageIndex == 0)
                {
                    // The reveal image is never counted -- numbering starts once the reader
                    // moves on to the title/body pages, so no counter shows on this one.
                    this.detailView.ShowImage(this.currentImage);
                    return;
                }

                int shownPage  = this.pageIndex;
                int shownTotal = total - 1;

                if (this.pageIndex == 1)
                {
                    this.detailView.ShowTitleOnly(this.currentTitle, shownPage, shownTotal, this.currentImage);
                    return;
                }

                this.detailView.ShowBodyOnly(this.currentPages[this.pageIndex - 2], shownPage, shownTotal, this.currentImage);
                return;
            }

            this.detailView.Show(this.currentTitle, this.currentPages[this.pageIndex], this.pageIndex + 1, total);
        }

        void AdvancePage()
        {
            this.pageIndex++;
            if (this.isVoiceNote && this.pageIndex >= this.NonBodyPageCount)
            {
                EnterVoiceBody();
                return;
            }
            if (this.pageIndex >= TotalPages)
            {
                this.detailView.Hide();
                return;
            }
            ShowCurrentPage();
        }

        void PreviousPage()
        {
            if (this.pageIndex <= 0) return;
            this.pageIndex--;
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
