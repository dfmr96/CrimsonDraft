#nullable enable

using UnityEngine;
using VContainer;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;

namespace CrimsonDraft.UI
{
    public sealed class PartyPanelView : MonoBehaviour
    {
        [SerializeField] private OperatorWidgetView[] widgets = System.Array.Empty<OperatorWidgetView>();

#if UNITY_EDITOR
        [Header("Standalone (sin VContainer)")]
        [SerializeField] private PartyTestHarness? testHarness;
#endif

        [Inject] private IOperatorRoster?   roster;
        [Inject] private IInventoryService? inventory;
        [Inject] private OperatorCorpseRegistry? corpseRegistry;

        public IOperatorRoster? Roster { get; private set; }

        // Bio page: the logo toggles it on every card at once, whichever card's logo was used.
        public bool BioMode { get; private set; }

        public void SetBioMode(bool on, bool animate = true)
        {
            if (this.BioMode == on) return;
            this.BioMode = on;
            foreach (var widget in this.widgets)
                if (widget != null) widget.SetBioMode(on, animate);
        }

        public void ToggleBio() => SetBioMode(!this.BioMode);

        void Start()
        {
            IOperatorRoster? source = this.roster;

#if UNITY_EDITOR
            if (source == null && this.testHarness != null)
            {
                var standaloneRoster = new OperatorRoster(this.testHarness);
                standaloneRoster.EnsureInitialized();
                this.testHarness.RegisterRoster(standaloneRoster);
                source = standaloneRoster;
            }
            else if (source is OperatorRoster injectedRoster)
            {
                if (this.testHarness != null) this.testHarness.RegisterRoster(injectedRoster);
            }
#endif

            source?.EnsureInitialized();

            if (source == null || !source.IsInitialized) return;

            this.Roster = source;

            for (int i = 0; i < this.widgets.Length; i++)
            {
                if (i < source.Count)
                    this.widgets[i].Bind(source[i], CanLoot(i), CorpseRoomId(i));
                else
                    this.widgets[i].ShowMissing();
            }
        }

        private bool CanLoot(int slot) => this.inventory != null && this.inventory.IsAccessible(ContainerId.Operator(slot));

        private string? CorpseRoomId(int slot) =>
            this.corpseRegistry != null && this.corpseRegistry.TryGet(slot, out var entry) ? entry.RoomId : null;

        public OperatorWidgetView? GetWidget(int index) =>
            index >= 0 && index < this.widgets.Length ? this.widgets[index] : null;

        public void Refresh()
        {
            if (this.roster == null || !this.roster.IsInitialized) return;
            for (int i = 0; i < this.widgets.Length; i++)
                if (i < this.roster.Count)
                    this.widgets[i].Bind(this.roster[i], CanLoot(i), CorpseRoomId(i));
        }
    }
}
