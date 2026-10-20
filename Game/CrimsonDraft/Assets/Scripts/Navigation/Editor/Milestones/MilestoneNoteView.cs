#nullable enable

using UnityEditor.Experimental.GraphView;
using UnityEngine;
using CrimsonDraft.Navigation.Milestones;

namespace CrimsonDraft.Navigation.Editor.Milestones
{
    // Sticky note ("Player choice", "Must have Beeper"). Its rect is written into the data as it
    // moves or resizes, since GetPosition() is zero until the view is laid out.
    public sealed class MilestoneNoteView : StickyNote
    {
        private readonly MilestoneNote note;
        private readonly MilestoneGraphView owner;

        public MilestoneNoteView(MilestoneNote note, MilestoneGraphView owner) : base(note.rect.position)
        {
            this.note  = note;
            this.owner = owner;
            title    = "";
            contents = note.text;
            SetPosition(note.rect);
            RegisterCallback<StickyNoteChangeEvent>(_ =>
            {
                this.note.text = contents;
                this.owner.ScheduleSave();
            });
        }

        public override void SetPosition(Rect newPos)
        {
            base.SetPosition(newPos);
            if (this.note == null) return; // base constructor positions before our fields are set
            var size = newPos.width > 0f && newPos.height > 0f ? newPos.size : this.note.rect.size;
            this.note.rect = new Rect(newPos.position, size);
            this.owner.ScheduleSave();
        }

        public MilestoneNote Capture()
        {
            this.note.text = contents;
            return this.note;
        }
    }
}
