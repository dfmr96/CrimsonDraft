#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using MessagePipe;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Infrastructure.Save.UI;
using Yarn.Unity;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.UI
{
    public sealed class FilesTabController : MonoBehaviour, IInitializable, IDisposable
    {
        [SerializeField] private NoteDatabase      database      = null!;
        [SerializeField] private NoteDetailView    detailView    = null!;
        [SerializeField] private NoteListView      list          = null!;
        [SerializeField] private TMP_Text?         previewLabel;

        [Header("Carousel")]
        [Tooltip("Los 3 modelos 3D (uno por categoria) que se ciclan antes de entrar a la grilla.")]
        [SerializeField] private NoteCarouselView? carousel;
        [Tooltip("La ventana de la grilla de notas: es la que hace pop desde el modelo seleccionado.")]
        [SerializeField] private RectTransform?    windowRoot;
        [SerializeField] private float             windowPopDuration = 0.45f;
        [SerializeField, Range(0f, 1f)] private float windowPopStartScale = 0.05f;
        [SerializeField] private TMP_Text?         categoryLabel;
        [SerializeField] private GameObject?       prevArrow;
        [SerializeField] private GameObject?       nextArrow;

        [Header("Highlights")]
        [SerializeField] private GameObject? carouselHighlight;
        [SerializeField] private GameObject? prevArrowHighlight;
        [SerializeField] private GameObject? nextArrowHighlight;
        [SerializeField] private float       arrowFlashTime = 0.12f;

        // Press feedback on the arrows, added at runtime (see EnsureArrowFeedback).
        private ManualSelectScale? prevArrowScale;
        private ManualSelectScale? nextArrowScale;

        [Header("List")]
        [Tooltip("Lo que se ve en la lista en lugar del nombre de una nota que todavia no se encontro. Cada nota tiene su lugar fijo, en el orden de la base de datos.")]
        [SerializeField] private string            missingNoteLabel = "????";

        [Header("Navigation Feel")]
        [SerializeField] private float initialRepeatDelay = 0.4f;
        [SerializeField] private float repeatInterval     = 0.12f;

        [Header("Voice Note Playback")]
        [SerializeField] private float voiceCharsPerSecond  = 18f;
        [Tooltip("Segundos que suena la cinta al reves cuando se vuelve a un parrafo anterior, antes de retomar normal.")]
        [SerializeField] private float voiceRewindSeconds   = 0.8f;
        [SerializeField] private float voiceScrubMultiplier = 4f;
        [Tooltip("Minimum real-time gap between typewriter ticks -- caps the tick rate independently of reveal speed, so holding Confirm to fast-forward (voiceScrubMultiplier) never turns it into a machine-gun.")]
        [SerializeField] private float voiceTypewriterMinInterval = 0.35f;

        [SerializeField] private YarnProject yarnProject = null!;

        [Header("Note Layout")]
        [Tooltip("Page-split limits plus spacing/alignment for note text. Empty = old behavior (no auto-split, TMP defaults).")]
        [SerializeField] private NoteLayoutSettings? layoutSettings;

        [Inject] private IInputService                    inputService         = null!;
        [Inject] private TabManager                       tabManager           = null!;
        [Inject] private InventoryOpenCloseController     openCloseController  = null!;
        [Inject] private ISubscriber<NoteCollectedEvent>   noteCollectedSub     = null!;
        [Inject] private InventorySfxData                 sfx                  = null!;
        [Inject] private NoteRegistry                      noteRegistry         = null!;

        private IDisposable? noteSubscription;

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

        // Notes are read straight from the localization table, so Yarn's own [important]...[/important]
        // markup (never parsed here) would show up literally. Same green as the DialogueMarkupPalette.
        private static readonly System.Text.RegularExpressions.Regex ImportantTextPattern =
            new(@"\[important\](.+?)\[/important\]",
                System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        const string ImportantTextColor = "#9A9F5C"; // the UI's olive

        static string ApplyImportantMarkup(string text) =>
            ImportantTextPattern.Replace(text, $"<color={ImportantTextColor}>$1</color>");

        static string ApplyTextMarkup(string text) =>
            ApplyImportantMarkup(ApplyItalicMarkup(ApplyDamagedTextMarkup(text)));

        // Order of the carousel (and of the models in NoteCarouselView): Voice Notes, Notes, Posters.
        private static readonly DocumentCategory[] Categories =
        {
            DocumentCategory.VoiceNotes,
            DocumentCategory.Notes,
            DocumentCategory.Posters,
        };

        private enum Mode { Carousel, Window }
        private Mode mode;

        private int categoryIndex = 1;
        private int selectedIndex;

        // The note window: pops out of the selected model and back into it.
        private Tween?  windowTween;
        private int     windowGeneration;
        private bool    windowPoseCached;
        private Vector3 windowRestPosition;
        private Vector3 windowRestScale = Vector3.one;

        // One fixed row per note of the open category, in the order of the database: a note keeps its
        // place whether or not it has been found yet (its row reads "????" until then).
        private          DocumentData[]         slots = Array.Empty<DocumentData>();

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

        // Cassette audio of the voice note being read (see InventorySfxData.StartVoiceTape).
        private bool       tapePlaying;
        private bool       tapeFast;
        private Coroutine? rewindRoutine;
        private int     voiceParagraphIndex;
        private float   voiceCharsShown;
        private float   voiceTotalDuration;
        private float[] voiceParagraphDurations = Array.Empty<float>();
        private bool    voiceAccelerating;
        private bool    voiceAccelArmed;  // Confirm must be released once after entering the body before it can fast-forward
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

        // direction: +1 while fast-forwarding (Confirm held), 0 otherwise -- the arrow is drawn
        // larger so it reads as a pressed button. Rewinding no longer exists.
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

        public NoteDatabase Database => this.database;

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
            this.openCloseController.Open(() =>
            {
                // Then force TabManager's first-time setup synchronously, in case Unity hasn't
                // gotten around to calling its Start() yet — otherwise the deferred Start() would
                // reset the active tab back to "Inventory" right after we switch to "Files".
                this.tabManager.EnsureInitialized();
                this.tabManager.ActivateTabByName("Files");
                OpenCollected(doc);
            });
        }

        /// <summary>Opens a note exactly as if the player had navigated to it by hand.</summary>
        public void OpenCollected(DocumentData doc)
        {
            int cat = Array.IndexOf(Categories, doc.Category);
            this.categoryIndex = cat >= 0 ? cat : 0;
            this.carousel?.Select(this.categoryIndex, animated: false);
            RefreshCategoryLabel();

            OpenWindow(animated: false);

            int idx = Array.IndexOf(this.slots, doc);
            if (idx >= 0)
            {
                this.selectedIndex = idx;
                PlaceSelectorAt(this.selectedIndex);
            }

            OpenNote(doc);
        }

        // ── Lifecycle ────────────────────────────────────────────────────────

        void Start()
        {
            EnsureArrowFeedback();
        }

        private bool arrowFeedbackReady;

        // Adds the press-feedback scale and the "<"/">" glyph to prevArrow/nextArrow purely at
        // runtime -- no scene wiring, so this doesn't depend on the scene being re-saved.
        // Idempotent: GetComponent/transform.Find pick up whatever a previous run already
        // added, so calling this again is a no-op.
        void EnsureArrowFeedback()
        {
            if (this.arrowFeedbackReady) return;
            this.arrowFeedbackReady = true;

            this.prevArrowScale = EnsureArrowScale(this.prevArrow);
            this.nextArrowScale = EnsureArrowScale(this.nextArrow);
            EnsureArrowGlyph(this.prevArrow, "<");
            EnsureArrowGlyph(this.nextArrow, ">");
        }

        static ManualSelectScale? EnsureArrowScale(GameObject? arrow) =>
            arrow == null ? null : arrow.GetComponent<ManualSelectScale>() ?? arrow.AddComponent<ManualSelectScale>();

        // Reuses categoryLabel's own font (Pixellari) instead of a new serialized font
        // reference, so this glyph never needs anything assigned in the Inspector either.
        void EnsureArrowGlyph(GameObject? arrow, string glyph)
        {
            if (arrow == null || this.categoryLabel == null) return;

            const string GlyphName = "ArrowGlyph";
            Transform existing = arrow.transform.Find(GlyphName);
            TMP_Text label;

            if (existing != null)
            {
                label = existing.GetComponent<TMP_Text>();
            }
            else
            {
                var go = new GameObject(GlyphName, typeof(RectTransform));
                go.transform.SetParent(arrow.transform, false);
                go.layer = arrow.layer;

                var rt = (RectTransform)go.transform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                label                    = go.AddComponent<TextMeshProUGUI>();
                label.font               = this.categoryLabel.font;
                label.fontSharedMaterial = this.categoryLabel.fontSharedMaterial;
                label.fontSize           = 24f;
                label.alignment          = TextAlignmentOptions.Center;
                label.color              = Color.white;
                label.raycastTarget      = false;
            }

            label.text = glyph;
        }

        void OnEnable()
        {
            if (this.inputService == null) return;
            this.inputService.InventoryConfirm.performed += OnConfirm;

            this.mode            = Mode.Carousel;
            this.categoryIndex   = Mathf.Clamp(this.categoryIndex, 0, Categories.Length - 1);
            this.selectedIndex   = 0;
            this.holding         = false;
            this.lastDir         = Vector2Int.zero;
            this.wasTabBarActive = true;

            this.list.SetSelectionVisible(false);
            ResetWindow();
            this.carousel?.Select(this.categoryIndex, animated: false);
            SetCarouselFocus(true);
            RefreshCategoryLabel();
        }

        void OnDisable()
        {
            this.windowTween?.Kill();
            StopVoiceTape();
            if (this.inputService == null) return;
            this.inputService.InventoryConfirm.performed -= OnConfirm;
        }

        void Update()
        {
            // The tape belongs to the open voice transcript: whatever closes it (Cancel, the last
            // paragraph, stepping back to the title page) also silences it.
            if (this.tapePlaying && (!this.detailView.IsOpen || !this.inVoiceBody))
                StopVoiceTape();

            if (this.tabManager == null) return;
            bool tabBarNow = this.tabManager.IsTabBarActive;
            if (tabBarNow != this.wasTabBarActive)
            {
                this.wasTabBarActive = tabBarNow;
                if (tabBarNow)
                {
                    this.list.SetSelectionVisible(false);
                    SetCarouselFocus(false);
                }
                else if (this.mode == Mode.Window) PlaceSelectorAt(this.selectedIndex);
                else                               SetCarouselFocus(true);
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
            this.voiceAccelerating   = false;
            // Whatever press brought us here is probably still down -- don't let it count as a
            // fast-forward until it has been released.
            this.voiceAccelArmed     = !this.inputService.InventoryConfirm.IsPressed();
            this.voiceLastDirX       = ReadDirection().x;
            this.voiceLastTypedChars = 0;
            this.voiceLastTypewriterTime = -999f;
            StartVoiceTape();
            ShowVoiceParagraph();
        }

        void StartVoiceTape()
        {
            StopVoiceTape();
            this.tapePlaying = true;
            this.tapeFast    = false;
            this.sfx?.StartVoiceTape(gameObject);
        }

        void StopVoiceTape()
        {
            if (this.rewindRoutine != null)
            {
                StopCoroutine(this.rewindRoutine);
                this.rewindRoutine = null;
            }
            if (!this.tapePlaying) return;

            this.tapePlaying = false;
            this.tapeFast    = false;
            this.sfx?.StopVoiceTape(gameObject);
        }

        // Going back a paragraph: the tape runs backwards for a moment, then carries on normally.
        void BeginVoiceRewind()
        {
            if (!this.tapePlaying) return;

            if (this.rewindRoutine != null) StopCoroutine(this.rewindRoutine);
            else this.sfx?.BeginVoiceTapeRewind(gameObject);

            this.rewindRoutine = StartCoroutine(VoiceRewindRoutine());
        }

        IEnumerator VoiceRewindRoutine()
        {
            yield return new WaitForSecondsRealtime(this.voiceRewindSeconds);
            this.rewindRoutine = null;
            this.sfx?.EndVoiceTapeRewind(gameObject);
        }

        // Runs every frame while a voice note's transcript is on screen. Each paragraph is its
        // own page: moving to another one always starts from a blank body label (or, going back,
        // shows the already-read paragraph in full), so the outgoing text never lingers.
        // Controls mirror a normal note:
        //   Left/Right  -- previous/next paragraph, one press = one paragraph (no repeat).
        //   Confirm     -- HOLD to fast-forward the typing; releasing returns to normal speed.
        //   Confirm     -- once the paragraph has finished typing, one fresh press moves to the
        //                  next paragraph (or closes the note after the last). Holding it through
        //                  the end never advances by itself: it has to be released and pressed
        //                  again (see OnConfirm, which only fires on the press edge).
        void UpdateVoicePlayback()
        {
            Vector2Int dir = ReadDirection();

            bool fresh = dir.x != 0 && dir.x != this.voiceLastDirX;
            this.voiceLastDirX = dir.x;

            if (fresh)
            {
                StepVoiceParagraph(dir.x > 0 ? 1 : -1);
                if (!this.inVoiceBody) return; // stepped back out to the title/image page
            }

            int  totalVisible = CountVisibleChars(this.currentPages[this.voiceParagraphIndex]);
            bool pressed      = this.inputService.InventoryConfirm.IsPressed();
            if (!pressed) this.voiceAccelArmed = true;

            bool atEnd = this.voiceCharsShown >= totalVisible;
            this.voiceAccelerating = pressed && this.voiceAccelArmed && !atEnd;

            // Fast-forwarding the text speeds the tape up too (and slows it back down on release).
            if (this.voiceAccelerating != this.tapeFast)
            {
                this.tapeFast = this.voiceAccelerating;
                this.sfx?.SetVoiceTapeFast(this.tapeFast);
            }

            float rate = this.voiceAccelerating ? this.voiceScrubMultiplier : 1f;
            this.voiceCharsShown = Mathf.Min(
                totalVisible,
                this.voiceCharsShown + rate * this.voiceCharsPerSecond * Time.unscaledDeltaTime);

            ShowVoiceParagraph();
        }

        // delta: +1 next paragraph, -1 previous; either way the target starts blank and types
        // itself out again. Before the first paragraph, -1 falls back to the title page (the
        // page right before the body) if there is one, else does nothing.
        void StepVoiceParagraph(int delta)
        {
            int target = this.voiceParagraphIndex + delta;

            if (target >= this.currentPages.Length) return;

            if (target < 0)
            {
                if (this.pageIndex <= 0) return;
                this.inVoiceBody = false;
                this.pageIndex--;

                // Hand the still-held Left over to UpdateDetailPaging as already consumed --
                // otherwise it sees a "new" press on its first frame and pages straight on
                // from the title to the image. Needs a release + new press to go further.
                this.lastDir      = ReadDirection();
                this.holding      = false;
                this.nextMoveTime = Time.unscaledTime + this.initialRepeatDelay;

                ShowCurrentPage();
                return;
            }

            this.voiceParagraphIndex = target;
            this.voiceCharsShown     = 0f;
            this.voiceLastTypedChars = 0;

            if (delta < 0) BeginVoiceRewind();
        }

        void ShowVoiceParagraph()
        {
            string paragraph    = this.currentPages[this.voiceParagraphIndex];
            int    totalVisible = CountVisibleChars(paragraph);
            int    shown        = Mathf.Clamp(Mathf.FloorToInt(this.voiceCharsShown), 0, totalVisible);

            // No typewriter tick here: a voice note's only sound is the cassette (see StartVoiceTape).
            this.voiceLastTypedChars = Mathf.Max(this.voiceLastTypedChars, shown);

            float elapsedBefore = 0f;
            for (int i = 0; i < this.voiceParagraphIndex; i++)
                elapsedBefore += this.voiceParagraphDurations[i];

            float remaining = this.voiceTotalDuration - elapsedBefore - (this.voiceCharsShown / this.voiceCharsPerSecond);
            remaining = Mathf.Clamp(remaining, 0f, this.voiceTotalDuration);

            string timerText       = FormatVoiceTimer(remaining, this.voiceTotalDuration, this.voiceAccelerating ? 1 : 0);
            bool   showTitleInline = this.currentImage == null;

            this.detailView.ShowVoiceBody(this.currentTitle, paragraph, shown, timerText, showTitleInline, this.currentImage);
        }

        // ── Navigation ───────────────────────────────────────────────────────

        void ProcessMove(Vector2Int dir)
        {
            if (this.mode == Mode.Carousel)
            {
                // The carousel is the topmost row of this tab: Up goes into the tab bar, same as
                // GridCursor exiting its grid from row 0.
                if (dir.y > 0)
                {
                    this.tabManager.EnterTabBar();
                    return;
                }
                MoveCarousel(dir);
            }
            else
            {
                MoveList(dir);
            }

            this.sfx?.PlayCursor(gameObject);
        }

        void MoveCarousel(Vector2Int dir)
        {
            if (dir.x == 0) return;

            this.categoryIndex = (this.categoryIndex + dir.x + Categories.Length) % Categories.Length;
            this.carousel?.Select(this.categoryIndex, animated: true);
            RefreshCategoryLabel();

            bool goingPrev = dir.x < 0;
            FlashArrowHighlight(goingPrev ? this.prevArrowHighlight : this.nextArrowHighlight);
            (goingPrev ? this.prevArrowScale : this.nextArrowScale)?.Punch();
        }

        void MoveList(Vector2Int dir)
        {
            if (this.slots.Length == 0 || dir.y == 0) return;

            // Up/Down walk the list one row at a time; it stops at both ends.
            int next = this.selectedIndex + (dir.y > 0 ? -1 : 1);
            if (next < 0 || next >= this.slots.Length) return;

            this.selectedIndex = next;
            PlaceSelectorAt(this.selectedIndex);
        }

        // ── Input callbacks ──────────────────────────────────────────────────

        void OnConfirm(InputAction.CallbackContext _)
        {
            if (this.tabManager.IsTabBarActive) return;

            if (this.detailView.IsOpen)
            {
                if (this.inVoiceBody)
                {
                    // 'performed' fires on the press only, so holding Confirm to fast-forward
                    // (see UpdateVoicePlayback) can't also page: advancing needs a fresh press
                    // after the paragraph has finished typing.
                    int total = CountVisibleChars(this.currentPages[this.voiceParagraphIndex]);
                    if (this.voiceCharsShown < total) return;

                    if (this.voiceParagraphIndex < this.currentPages.Length - 1)
                    {
                        StepVoiceParagraph(1);
                        ShowVoiceParagraph();
                    }
                    else
                    {
                        this.detailView.Hide();
                    }
                    return;
                }

                AdvancePage();
                return;
            }

            if (this.mode == Mode.Carousel)
            {
                this.sfx?.PlayDecide(gameObject);
                OpenWindow(animated: true);
                return;
            }

            if (this.slots.Length == 0) return;

            // A note that has not been found yet is only a placeholder: nothing to open.
            var doc = this.slots[this.selectedIndex];
            if (!string.IsNullOrEmpty(doc.NoteId) && this.noteRegistry.IsCollected(doc.NoteId))
            {
                // Paper rustle for a note; a voice note is a cassette, its own tape sound starts
                // once the transcript opens.
                if (doc.Category != DocumentCategory.VoiceNotes)
                    this.sfx?.PlayFileOpen(gameObject);
                OpenNote(doc);
            }
        }

        // Called by TabManager.OnCancelTab — the sole subscriber to InventoryCancel — so the
        // open note detail view gets first chance to consume Cancel before TabManager falls
        // back to entering the tab bar selector.
        public bool TryConsumeCancel()
        {
            if (this.detailView.IsOpen)
            {
                this.sfx?.PlayCancel(gameObject);
                this.detailView.Hide();
                return true;
            }

            // The note window closes back into its model before Cancel leaves for the tab bar.
            if (this.mode == Mode.Window)
            {
                this.sfx?.PlayCancel(gameObject);
                CloseWindow();
                return true;
            }

            return false;
        }

        void OpenNote(DocumentData doc)
        {
            if (this.yarnProject == null) return;

            const string PageBreak = "<page>";

            var lineIds      = this.yarnProject.GetLineIDsForNodes(new[] { doc.NoteId });
            var localization = this.yarnProject.baseLocalization;
            var pages        = new List<string>();
            var current      = new System.Text.StringBuilder();
            int currentLines = 0;

            // Voice notes split too: each page there is one transcript paragraph, and playback
            // time is derived from character counts, so more/shorter paragraphs keep the same
            // total duration.
            int maxLines = this.layoutSettings == null
                ? int.MaxValue
                : this.layoutSettings.ResolveMaxYarnLines(doc);
            int maxVisualLines = this.layoutSettings == null
                ? int.MaxValue
                : this.layoutSettings.ResolveMaxRows(doc);

            // The label must already carry this note's font/alignment, or the row measurement
            // below would wrap with the previous note's settings.
            this.detailView.SetLayout(this.layoutSettings, doc);

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
                        currentLines = 0;
                    }
                }
                else
                {
                    var marked = ApplyTextMarkup(text);

                    // Page is full -- continue on a new one instead of cramming more lines in.
                    // Full means too many yarn lines, or (measured with the real label, since a
                    // yarn line wraps into several rows) too many rendered rows once this line
                    // is added. A page's first line is always accepted, however long.
                    if (currentLines > 0 &&
                        (currentLines >= maxLines ||
                         (maxVisualLines != int.MaxValue &&
                          this.detailView.MeasureLineCount(current + "\n" + marked) > maxVisualLines)))
                    {
                        pages.Add(current.ToString());
                        current.Clear();
                        currentLines = 0;
                    }

                    if (current.Length > 0) current.Append('\n');
                    current.Append(marked);
                    currentLines++;
                }
            }

            if (current.Length > 0)
                pages.Add(current.ToString());

            if (this.layoutSettings != null && this.layoutSettings.MaxPages > 0 && pages.Count > this.layoutSettings.MaxPages)
                Debug.LogWarning($"[Notes] '{doc.NoteId}' has {pages.Count} pages (max {this.layoutSettings.MaxPages}). Consider trimming it.", doc);

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
            PlayPageTurnSound();
            ShowPageTurned(+1);
        }

        void PreviousPage()
        {
            if (this.pageIndex <= 0) return;
            this.pageIndex--;
            PlayPageTurnSound();
            ShowPageTurned(-1);
        }

        // A plain note's page slides away and the next one slides in; a voice transcript just changes.
        void ShowPageTurned(int direction)
        {
            if (this.isVoiceNote) ShowCurrentPage();
            else                  this.detailView.TurnPage(direction, ShowCurrentPage);
        }

        // Paper rustle for turning the page of a normal note. Voice notes never play it: their
        // title/image pages and transcript paragraphs aren't sheets of paper.
        void PlayPageTurnSound()
        {
            if (this.isVoiceNote) return;
            this.sfx?.PlayPageTurn(gameObject);
        }

        // ── Grid ─────────────────────────────────────────────────────────────

        void RebuildList()
        {
            // Every note of the open category gets its own row, in database order; gaps in the
            // database list (unassigned entries) are skipped rather than left as holes.
            var category = Categories[this.categoryIndex];
            this.slots = this.database.Notes.Where(n => n != null && n.Category == category).ToArray();

            var labels = new string[this.slots.Length];
            var found  = new bool[this.slots.Length];
            for (int i = 0; i < this.slots.Length; i++)
            {
                found[i]  = this.noteRegistry.IsCollected(this.slots[i].NoteId);
                labels[i] = found[i] ? this.slots[i].Title : this.missingNoteLabel;
            }

            this.list.Bind(labels, found);
            this.selectedIndex = Mathf.Clamp(this.selectedIndex, 0, Mathf.Max(0, this.slots.Length - 1));
        }

        // ── Category / window ────────────────────────────────────────────────

        void RefreshCategoryLabel()
        {
            if (this.categoryLabel != null) this.categoryLabel.text = DisplayName(Categories[this.categoryIndex]);
        }

        static string DisplayName(DocumentCategory cat) => cat switch
        {
            DocumentCategory.Notes      => "Notes",
            DocumentCategory.VoiceNotes => "Voice Notes",
            DocumentCategory.Posters    => "Posters",
            _                           => cat.ToString()
        };

        void SetCarouselFocus(bool on) => this.carouselHighlight?.SetActive(on);

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

        void CacheWindowPose(RectTransform rect)
        {
            if (this.windowPoseCached) return;
            this.windowRestPosition = rect.localPosition;
            this.windowRestScale    = rect.localScale;
            this.windowPoseCached   = true;
        }

        // Window hidden, pose back to rest (tab enabled / interrupted close).
        void ResetWindow()
        {
            this.windowGeneration++;
            this.windowTween?.Kill();
            this.carousel?.SetDimmed(false, instant: true);
            if (this.windowRoot == null) return;

            if (this.windowPoseCached)
            {
                this.windowRoot.localPosition = this.windowRestPosition;
                this.windowRoot.localScale    = this.windowRestScale;
            }
            this.windowRoot.gameObject.SetActive(false);
        }

        // The window's collapsed pose: centred on the selected model, tiny.
        void ComputeWindowCollapsed(RectTransform rect, out Vector3 position, out Vector3 scale)
        {
            position = this.windowRestPosition;
            scale    = this.windowRestScale * this.windowPopStartScale;
            if (this.carousel != null && rect.parent != null && this.carousel.TryGetSelectedAnchor(out var world))
            {
                position   = rect.parent.InverseTransformPoint(world);
                position.z = this.windowRestPosition.z;
            }
        }

        void OpenWindow(bool animated)
        {
            this.mode = Mode.Window;
            this.selectedIndex = 0;
            RebuildList();
            this.carousel?.SetDimmed(true);
            SetCarouselFocus(false);

            var rect = this.windowRoot;
            if (rect == null)
            {
                PlaceSelectorAt(this.selectedIndex);
                return;
            }

            this.windowGeneration++;
            this.windowTween?.Kill();
            CacheWindowPose(rect);
            rect.gameObject.SetActive(true);
            PlaceSelectorAt(this.selectedIndex);

            if (!animated || !rect.gameObject.activeInHierarchy)
            {
                rect.localPosition = this.windowRestPosition;
                rect.localScale    = this.windowRestScale;
                return;
            }

            // One eased progress drives both position and scale: the window grows out of the model
            // along a straight line (separate eases would make it grow first and then slide).
            ComputeWindowCollapsed(rect, out var start, out var startScale);
            rect.localPosition = start;
            rect.localScale    = startScale;

            this.windowTween = DOVirtual.Float(0f, 1f, this.windowPopDuration, v =>
                {
                    rect.localPosition = Vector3.LerpUnclamped(start, this.windowRestPosition, v);
                    rect.localScale    = Vector3.LerpUnclamped(startScale, this.windowRestScale, v);
                })
                .SetEase(Ease.OutCubic).SetUpdate(true).SetTarget(this);
        }

        void CloseWindow()
        {
            if (this.mode != Mode.Window) return;
            this.mode = Mode.Carousel;
            this.list.SetSelectionVisible(false);
            this.carousel?.SetDimmed(false);
            SetCarouselFocus(true);

            var rect = this.windowRoot;
            if (rect == null) return;

            int generation = ++this.windowGeneration;
            this.windowTween?.Kill();

            if (!this.windowPoseCached || !rect.gameObject.activeInHierarchy)
            {
                rect.gameObject.SetActive(false);
                return;
            }

            Vector3 from      = rect.localPosition;
            Vector3 fromScale = rect.localScale;
            ComputeWindowCollapsed(rect, out var end, out var endScale);

            this.windowTween = DOVirtual.Float(0f, 1f, this.windowPopDuration, v =>
                {
                    rect.localPosition = Vector3.LerpUnclamped(from, end, v);
                    rect.localScale    = Vector3.LerpUnclamped(fromScale, endScale, v);
                })
                .SetEase(Ease.InCubic).SetUpdate(true).SetTarget(this)
                .OnKill(() =>
                {
                    if (generation != this.windowGeneration) return;
                    rect.gameObject.SetActive(false);
                    rect.localPosition = this.windowRestPosition;
                    rect.localScale    = this.windowRestScale;
                });
        }

        // ── Selector ─────────────────────────────────────────────────────────

        void PlaceSelectorAt(int index)
        {
            if (index < 0 || index >= this.slots.Length)
            {
                this.list.SetSelectionVisible(false);
                return;
            }

            this.list.Select(index);

            if (this.previewLabel != null)
            {
                int found = this.slots.Count(n => this.noteRegistry.IsCollected(n.NoteId));
                this.previewLabel.text = $"{found}/{this.slots.Length}";
            }
        }

        // ── Tab bar integration ───────────────────────────────────────────────

        public void HideSelectorForTabBar()
        {
            this.list.SetSelectionVisible(false);
            this.holding = false;
            this.lastDir = Vector2Int.zero;
        }

        public void ShowSelectorAfterTabBar()
        {
            if (this.mode == Mode.Window) PlaceSelectorAt(this.selectedIndex);
            else                          this.list.SetSelectionVisible(false);
            SetCarouselFocus(this.mode == Mode.Carousel);
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
