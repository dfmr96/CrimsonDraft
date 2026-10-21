#nullable enable

using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using CrimsonDraft.Navigation.Milestones;

namespace CrimsonDraft.Navigation.Editor.Milestones
{
    public sealed class MilestoneGraphWindow : EditorWindow
    {
        [SerializeField] private MilestoneGraph? graph;
        private MilestoneGraphView? view;

        [MenuItem("Tools/CrimsonDraft/Milestone Graph")]
        public static void Open() => GetWindow<MilestoneGraphWindow>("Milestone Graph");

        [OnOpenAsset]
        public static bool OnOpenAsset(int instanceId, int line)
        {
            // Must be the asset actually being opened, not merely the current selection: other
            // open requests (Console double-click, Edit Script…) arrive here too.
#pragma warning disable CS0618 // InstanceIDToObject: OnOpenAsset still hands us an instance id
            if (EditorUtility.InstanceIDToObject(instanceId) is not MilestoneGraph asset) return false;
#pragma warning restore CS0618
            var window = GetWindow<MilestoneGraphWindow>("Milestone Graph");
            window.SetGraph(asset);
            return true;
        }

        private void CreateGUI() => Rebuild();

        private void OnEnable()  => Undo.undoRedoPerformed += OnUndoRedo;
        private void OnDisable() => Undo.undoRedoPerformed -= OnUndoRedo;

        private void OnUndoRedo() => this.view?.Load();

        // Picks up lane colours edited in the Inspector, without rebuilding (keeps the selection).
        private void OnFocus() => this.view?.RefreshLaneColors();

        private void SetGraph(MilestoneGraph? asset)
        {
            this.graph = asset;
            Rebuild();
        }

        private void Rebuild()
        {
            rootVisualElement.Clear();

            var toolbar = new Toolbar();
            var field = new ObjectField { objectType = typeof(MilestoneGraph), value = this.graph };
            field.RegisterValueChangedCallback(e => SetGraph(e.newValue as MilestoneGraph));
            toolbar.Add(field);
            toolbar.Add(new ToolbarButton(() => this.view?.FrameAll()) { text = "Frame All" });
            // Lanes (name + colour) are edited in the asset's Inspector; OnFocus reloads the view.
            toolbar.Add(new ToolbarButton(() => Selection.activeObject = this.graph) { text = "Edit Lanes" });

            var search = new ToolbarSearchField();
            search.RegisterValueChangedCallback(e =>
            {
                if (this.view == null || string.IsNullOrWhiteSpace(e.newValue)) return;
                var match = this.view.nodes.OfType<MilestoneNodeView>()
                    .FirstOrDefault(n => n.Node.title.IndexOf(e.newValue, System.StringComparison.OrdinalIgnoreCase) >= 0);
                if (match == null) return;
                this.view.ClearSelection();
                this.view.AddToSelection(match);
                this.view.FrameSelection();
            });
            toolbar.Add(search);
            rootVisualElement.Add(toolbar);

            if (this.graph == null)
            {
                rootVisualElement.Add(new HelpBox("Pick or create a Milestone Graph asset (Create > CrimsonDraft > Milestone Graph).", HelpBoxMessageType.Info));
                return;
            }

            this.view = new MilestoneGraphView(this.graph);
            this.view.StretchToParentSize();
            var host = new VisualElement { style = { flexGrow = 1 } };
            host.Add(this.view);
            rootVisualElement.Add(host);
            this.view.Load();
        }
    }
}
