#nullable enable

using VContainer.Unity;
using CrimsonDraft.Infrastructure.Scenes;

namespace CrimsonDraft.Combat
{
    public sealed class BattlefieldPresenter : IInitializable
    {
        private readonly IEncounterContext  encounterContext;
        private readonly EncounterDatabase  encounterDatabase;
        private readonly IBattlefieldView   battlefieldView;

        [UnityEngine.Scripting.Preserve]
        public BattlefieldPresenter(
            IEncounterContext encounterContext,
            EncounterDatabase encounterDatabase,
            IBattlefieldView  battlefieldView)
        {
            this.encounterContext  = encounterContext;
            this.encounterDatabase = encounterDatabase;
            this.battlefieldView   = battlefieldView;
        }

        void IInitializable.Initialize()
        {
            var encounterId = this.encounterContext.CurrentEncounterId;
            if (encounterId == null) return;

            var encounter = this.encounterDatabase.GetById(encounterId);
            if (encounter == null) return;

            this.battlefieldView.Populate(encounter);
        }
    }
}
