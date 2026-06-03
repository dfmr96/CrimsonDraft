#nullable enable

using UnityEngine;
using VContainer;
using CrimsonDraft.Operators;

namespace CrimsonDraft.UI
{
    public sealed class PartyPanelView : MonoBehaviour
    {
        [SerializeField] private OperatorWidgetView[] widgets = System.Array.Empty<OperatorWidgetView>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Header("Standalone (sin VContainer)")]
        [SerializeField] private PartyTestHarness?    testHarness;
        [SerializeField] private OperatorTestWidget[] testWidgets = System.Array.Empty<OperatorTestWidget>();
#endif

        [Inject] private IOperatorRoster? roster;

        public IOperatorRoster? Roster { get; private set; }

        void Start()
        {
            IOperatorRoster? source = this.roster;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (source == null && this.testHarness != null)
            {
                var standaloneRoster = new OperatorRoster(this.testHarness);
                standaloneRoster.EnsureInitialized();
                this.testHarness.RegisterRoster(standaloneRoster);
                foreach (var w in this.testWidgets) w.RegisterRoster(standaloneRoster);
                source = standaloneRoster;
            }
            else if (source is OperatorRoster injectedRoster)
            {
                if (this.testHarness != null) this.testHarness.RegisterRoster(injectedRoster);
                foreach (var w in this.testWidgets) w.RegisterRoster(injectedRoster);
            }
#endif

            source?.EnsureInitialized();

            if (source == null || !source.IsInitialized) return;

            this.Roster = source;

            for (int i = 0; i < this.widgets.Length; i++)
            {
                if (i < source.Count)
                    this.widgets[i].Bind(source[i]);
                else
                    this.widgets[i].gameObject.SetActive(false);
            }
        }

        public OperatorWidgetView? GetWidget(int index) =>
            index >= 0 && index < this.widgets.Length ? this.widgets[index] : null;

        public void Refresh()
        {
            if (this.roster == null || !this.roster.IsInitialized) return;
            for (int i = 0; i < this.widgets.Length; i++)
                if (i < this.roster.Count)
                    this.widgets[i].Bind(this.roster[i]);
        }
    }
}
