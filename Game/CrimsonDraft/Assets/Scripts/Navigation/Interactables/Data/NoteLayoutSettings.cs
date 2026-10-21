#nullable enable

using System;
using TMPro;
using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    // Global reading rules for notes. NoteDetailView applies the spacing/alignment at display
    // time and FilesTabController uses the limits to split pages, so the note text itself
    // (Notes.yarn) stays plain: one yarn line = one "renglon", <page> = a manual page break.
    //
    // Precedence for alignment / rows / font: DocumentData override -> category rules -> globals.
    [CreateAssetMenu(menuName = "CrimsonDraft/Interactables/NoteLayoutSettings")]
    public sealed class NoteLayoutSettings : ScriptableObject
    {
        [Serializable]
        public sealed class CategoryRules
        {
            public DocumentCategory category = DocumentCategory.Posters;

            [Tooltip("UseDefault = follow the global default alignment.")]
            public NoteAlignment alignment = NoteAlignment.UseDefault;

            [Tooltip("Max rendered rows per page for this category. 0 = follow the global page rules.")]
            [Min(0)] public int maxRows;

            [Tooltip("Font for the title and body of this category. Empty = keep the font set on the labels.")]
            public TMP_FontAsset? font;
        }

        [Header("Page rules")]
        [Tooltip("Max yarn lines on one page. Longer notes are split into extra pages automatically.")]
        [SerializeField, Min(1)] private int maxLinesPerPage = 4;

        [Tooltip("Max rendered text rows on one page (a long yarn line wraps into several). A page is split between yarn lines once adding the next line would pass this. A single line longer than this still gets a page of its own.")]
        [SerializeField, Min(1)] private int maxVisualLinesPerPage = 6;

        [Tooltip("Max pages per note. 0 = no limit. Only warns (console + validator) -- text is never cut.")]
        [SerializeField, Min(0)] private int maxPages = 6;

        [Header("Spacing (TMP units, hundredths of an em: 100 = one full line)")]
        [Tooltip("Extra space between wrapped lines inside one yarn line.")]
        [SerializeField] private float lineSpacing = 30f;

        [Tooltip("Extra space between yarn lines (applied at each line break).")]
        [SerializeField] private float paragraphSpacing = 100f;

        [Header("Alignment")]
        [Tooltip("Used by every note whose DocumentData alignment is UseDefault and whose category has no rule.")]
        [SerializeField] private NoteAlignment defaultAlignment = NoteAlignment.Left;

        [Header("Font")]
        [Tooltip("Font for the title and body of every note. Empty = keep the font set on the labels. A category rule's font wins over this.")]
        [SerializeField] private TMP_FontAsset? font;

        [Tooltip("Body text size. 0 = keep the size set on the label.")]
        [SerializeField, Min(0)] private float bodyFontSize;

        [Tooltip("Title size (small and big title). 0 = keep the size set on the label.")]
        [SerializeField, Min(0)] private float titleFontSize;

        [Header("Per-category rules")]
        [SerializeField] private CategoryRules[] categoryRules = Array.Empty<CategoryRules>();

        public int   MaxPages         => this.maxPages;
        public float LineSpacing      => this.lineSpacing;
        public float ParagraphSpacing => this.paragraphSpacing;

        CategoryRules? RulesFor(DocumentCategory category)
        {
            foreach (var rules in this.categoryRules)
                if (rules.category == category) return rules;
            return null;
        }

        public NoteAlignment ResolveAlignment(DocumentData doc)
        {
            if (doc.Alignment != NoteAlignment.UseDefault) return doc.Alignment;

            var rules = RulesFor(doc.Category);
            if (rules != null && rules.alignment != NoteAlignment.UseDefault) return rules.alignment;

            return this.defaultAlignment == NoteAlignment.UseDefault ? NoteAlignment.Left : this.defaultAlignment;
        }

        /// <summary>Max rendered rows on one page of <paramref name="doc"/>; also caps its yarn lines per page.</summary>
        public int ResolveMaxRows(DocumentData doc)
        {
            if (doc.MaxRows > 0) return doc.MaxRows;

            var rules = RulesFor(doc.Category);
            return rules != null && rules.maxRows > 0 ? rules.maxRows : this.maxVisualLinesPerPage;
        }

        public int ResolveMaxYarnLines(DocumentData doc)
        {
            int rows = ResolveMaxRows(doc);
            // A per-note / per-category row count also lifts the yarn-line cap if it is higher.
            bool overridden = doc.MaxRows > 0 || (RulesFor(doc.Category)?.maxRows ?? 0) > 0;
            return overridden ? rows : this.maxLinesPerPage;
        }

        public float BodyFontSize  => this.bodyFontSize;
        public float TitleFontSize => this.titleFontSize;

        public TMP_FontAsset? ResolveFont(DocumentData doc)
        {
            var categoryFont = RulesFor(doc.Category)?.font;
            return categoryFont != null ? categoryFont : this.font;
        }
    }
}
