#nullable enable

using Cysharp.Threading.Tasks;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Navigation;
using CrimsonDraft.Navigation.Player;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Tests
{
    public sealed class RoomOrchestratorInitTests
    {
        [Test]
        public void Initialize_withStartingRoom_activatesOnlyThatRoom()
        {
            var goA    = new GameObject("RoomA"); goA.SetActive(true);
            var roomA  = goA.AddComponent<RoomController>();
            var goB    = new GameObject("RoomB"); goB.SetActive(true);
            goB.AddComponent<RoomController>();

            var playerGo = new GameObject("Player");
            var player   = playerGo.AddComponent<PlayerController>();
            var context  = ScriptableObject.CreateInstance<RoomTransitionContext>();
            context.SetStartingRoom(roomA);

            try
            {
                var orchestrator = MakeOrchestrator(player, context);
                ((IInitializable)orchestrator).Initialize();

                Assert.IsTrue(goA.activeSelf,  "starting room must be active");
                Assert.IsFalse(goB.activeSelf, "other rooms must be deactivated");
            }
            finally
            {
                Object.DestroyImmediate(goA);
                Object.DestroyImmediate(goB);
                Object.DestroyImmediate(playerGo);
                Object.DestroyImmediate(context);
            }
        }

        [Test]
        public void Initialize_withNoStartingRoom_activatesFirstFound()
        {
            var goA = new GameObject("RoomA"); goA.SetActive(false);
            goA.AddComponent<RoomController>();
            var goB = new GameObject("RoomB"); goB.SetActive(false);
            goB.AddComponent<RoomController>();

            var playerGo = new GameObject("Player");
            var player   = playerGo.AddComponent<PlayerController>();
            var context  = ScriptableObject.CreateInstance<RoomTransitionContext>();

            try
            {
                var orchestrator = MakeOrchestrator(player, context);
                ((IInitializable)orchestrator).Initialize();

                int activeCount = (goA.activeSelf ? 1 : 0) + (goB.activeSelf ? 1 : 0);
                Assert.AreEqual(1, activeCount, "exactly one room must be active when no starting room set");
            }
            finally
            {
                Object.DestroyImmediate(goA);
                Object.DestroyImmediate(goB);
                Object.DestroyImmediate(playerGo);
                Object.DestroyImmediate(context);
            }
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static RoomOrchestrator MakeOrchestrator(PlayerController player, RoomTransitionContext context)
            => new RoomOrchestrator(
                new FakeInputService(),
                player,
                context,
                new FakePublisher<RoomTransitionStartedEvent>(),
                new FakePublisher<RoomTransitionedEvent>());

        private sealed class FakeInputService : IInputService
        {
            public InputAction Move                   => null!;
            public InputAction Interact               => null!;
            public InputAction OpenInventory          => null!;
            public InputAction OpenMap                => null!;
            public InputAction Aim                    => null!;
            public InputAction Pause                  => null!;
            public InputAction Sprint                 => null!;
            public InputAction CombatNavigate         => null!;
            public InputAction CombatConfirm          => null!;
            public InputAction CombatCancel           => null!;
            public InputAction CombatUseItem          => null!;
            public InputAction UINavigate             => null!;
            public InputAction UIConfirm              => null!;
            public InputAction UICancel               => null!;
            public InputAction UIBack                 => null!;
            public InputAction DialogueAdvanceLine    => null!;
            public InputAction DialogueCancelDialogue => null!;
            public InputAction DoorTransitionSkip     => null!;
            public InputAction InventoryNavigate      => null!;
            public InputAction InventoryConfirm       => null!;
            public InputAction InventoryPickup        => null!;
            public InputAction InventoryCancel        => null!;
            public InputAction InventoryNextTab       => null!;
            public InputAction InventoryPrevTab       => null!;
            public void SwitchToGameplay()       { }
            public void SwitchToCombat()         { }
            public void SwitchToUI()             { }
            public void SwitchToDialogue()       { }
            public void SwitchToDoorTransition() { }
            public void SwitchToInventory()      { }
            public void Dispose()                { }
        }

        private sealed class FakePublisher<T> : IPublisher<T>
        {
            public void Publish(T message) { }
        }
    }
}
