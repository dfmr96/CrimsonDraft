#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    // Global reading rules for notes. NoteDetailView applies the spacing/alignment at display
    // time and FilesTabController uses the limits to split pages, so the note text itself
    // (Notes.yarn) stays plain: one yarn line = one "renglon", <page> = a manual page break.
    [CreateAssetMenu(menuName = "CrimsonDraft/Interactables/NoteLayoutSettings")]
    public sealed class NoteLayoutSettings : ScriptableObject
    {
        [Header("Page rules")]
        [Tooltip("Max yarn lines on one page. Longer notes are split into extra pages automatically.")]
        [SerializeField, Min(1)] private int maxLinesPerPage = 4;

        [Tooltip("Max pages per note. 0 = no limit. Only warns (console + validator) -- text is never cut.")]
        [SerializeField, Min(0)] private int maxPages = 6;

        [Header("Spacing (TMP units)")]
        [Tooltip("Extra space between wrapped lines inside one yarn line.")]
        [SerializeField] private float lineSpacing = 10f;

        [Tooltip("Extra space between yarn lines (applied at each line break).")]
        [SerializeField] private float paragraphSpacing = 24f;

        [Header("Alignment")]
        [Tooltip("Used by every note whose DocumentData alignment is UseDefault.")]
        [SerializeField] private NoteAlignment defaultAlignment = NoteAlignment.Left;

        public int   MaxLinesPerPage  => this.maxLinesPerPage;
        public int   MaxPages         => this.maxPages;
        public float LineSpacing      => this.lineSpacing;
        public float ParagraphSpacing => this.paragraphSpacing;

        public NoteAlignment Resolve(NoteAlignment noteAlignment)
        {
            if (noteAlignment != NoteAlignment.UseDefault) return noteAlignment;
            return this.defaultAlignment == NoteAlignment.UseDefault ? NoteAlignment.Left : this.defaultAlignment;
        }
    }
}
