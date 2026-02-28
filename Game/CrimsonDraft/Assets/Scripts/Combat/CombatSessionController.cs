#nullable enable

using System;
using MessagePipe;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Infrastructure.Input;

namespace CrimsonDraft.Combat
{
    public sealed class CombatSessionController : IInitializable, IDisposable
    {
        private readonly IInputService inputService;
        private readonly IPublisher<CombatEndedEvent> combatEndedPublisher;

        [Preserve]
        public CombatSessionController(
            IInputService inputService,
            IPublisher<CombatEndedEvent> combatEndedPublisher)
        {
            this.inputService = inputService;
            this.combatEndedPublisher = combatEndedPublisher;
        }

        void IInitializable.Initialize()
        {
            this.inputService.CombatCancel.performed += OnCombatCancel;
        }

        private void OnCombatCancel(InputAction.CallbackContext _) =>
            EndCombat(victory: false);

        public void EndCombat(bool victory)
        {
            this.inputService.CombatCancel.performed -= OnCombatCancel;
            this.combatEndedPublisher.Publish(new CombatEndedEvent { Victory = victory });
        }

        void IDisposable.Dispose()
        {
            this.inputService.CombatCancel.performed -= OnCombatCancel;
        }
    }
}
