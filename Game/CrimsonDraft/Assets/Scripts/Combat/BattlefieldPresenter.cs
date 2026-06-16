#nullable enable

using VContainer.Unity;
using CrimsonDraft.Infrastructure.Scenes;

namespace CrimsonDraft.Combat
{
    public sealed class BattlefieldPresenter : IInitializable
    {
        private readonly IEncounterContext encounterContext;
        private readonly IBattlefieldView  battlefieldView;

        [UnityEngine.Scripting.Preserve]
        public BattlefieldPresenter(
            IEncounterContext encounterContext,
            IBattlefieldView  battlefieldView)
        {
            this.encounterContext = encounterContext;
            this.battlefieldView  = battlefieldView;
        }

        void IInitializable.Initialize()
        {
            var encounter = this.encounterContext.EncounterAsset as EncounterData;
            if (encounter == null) return;

            this.battlefieldView.Populate(encounter);
        }
    }
}
