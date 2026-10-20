#nullable enable

using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Navigation.Milestones;

namespace CrimsonDraft.Navigation.Editor.Milestones
{
    // Kind dropdown + the ID picker that kind needs. Calls onChanged after every edit.
    public sealed class MilestoneTriggerField : VisualElement
    {
        private readonly MilestoneTrigger trigger;
        private readonly Action onChanged;
        private readonly VisualElement body = new();

        public MilestoneTriggerField(MilestoneTrigger trigger, Action onChanged)
        {
            this.trigger   = trigger;
            this.onChanged = onChanged;

            var kind = new EnumField("Trigger", trigger.kind);
            kind.RegisterValueChangedCallback(e =>
            {
                this.trigger.kind = (MilestoneTriggerKind)e.newValue;
                RebuildBody();
                this.onChanged();
            });
            Add(kind);
            Add(this.body);
            RebuildBody();
        }

        private void RebuildBody()
        {
            this.body.Clear();
            switch (this.trigger.kind)
            {
                case MilestoneTriggerKind.ItemHeld:
                {
                    var field = new ObjectField("Item") { objectType = typeof(ItemData), value = this.trigger.item };
                    field.RegisterValueChangedCallback(e => { this.trigger.item = e.newValue as ItemData; this.onChanged(); });
                    this.body.Add(field);
                    break;
                }
                case MilestoneTriggerKind.NoteRead:
                {
                    var field = new ObjectField("Note") { objectType = typeof(DocumentData), value = this.trigger.note };
                    field.RegisterValueChangedCallback(e => { this.trigger.note = e.newValue as DocumentData; this.onChanged(); });
                    this.body.Add(field);
                    break;
                }
                case MilestoneTriggerKind.PickupCollected:
                case MilestoneTriggerKind.SocketCompleted:
                case MilestoneTriggerKind.DoorUnlocked:
                case MilestoneTriggerKind.PuzzleSolved:
                {
                    var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                    var text = new TextField("Id") { value = this.trigger.targetId, style = { flexGrow = 1 } };
                    text.RegisterValueChangedCallback(e => { this.trigger.targetId = e.newValue; this.onChanged(); });
                    var pick = new Button(() => ShowIdMenu(text)) { text = "…" };
                    row.Add(text);
                    row.Add(pick);
                    this.body.Add(row);
                    break;
                }
            }
        }

        private void ShowIdMenu(TextField text)
        {
            var menu = new GenericMenu();
            var ids = MilestoneSceneIds.For(this.trigger.kind);
            if (ids.Count == 0)
                menu.AddDisabledItem(new GUIContent("No ids in open scenes"));
            foreach (var id in ids)
            {
                var captured = id;
                menu.AddItem(new GUIContent(captured), captured == this.trigger.targetId, () => text.value = captured);
            }
            menu.ShowAsContext();
        }
    }
}
