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
        public void Initialize_withOneActiveRoom_keepsItActive_andInactivesRemainInactive()
        {
            var goA = new GameObject("RoomA"); goA.SetActive(true);
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

                Assert.IsTrue(goA.activeSelf,  "active room must remain active");
                Assert.IsFalse(goB.activeSelf, "inactive room must remain inactive");
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
        public void Initialize_withMultipleActiveRooms_deactivatesAllButFirst()
        {
            var goA = new GameObject("RoomA"); goA.SetActive(true);
            goA.AddComponent<RoomController>();
            var goB = new GameObject("RoomB"); goB.SetActive(true);
            goB.AddComponent<RoomController>();

            var playerGo = new GameObject("Player");
            var player   = playerGo.AddComponent<PlayerController>();
            var context  = ScriptableObject.CreateInstance<RoomTransitionContext>();

            try
            {
                var orchestrator = MakeOrchestrator(player, context);
                ((IInitializable)orchestrator).Initialize();

                int activeCount = (goA.activeSelf ? 1 : 0) + (goB.activeSelf ? 1 : 0);
                Assert.AreEqual(1, activeCount, "exactly one room must be active after initialize");
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
            public void SwitchToGameplay() { }
            public void SwitchToCombat()   { }
            public void SwitchToUI()       { }
            public void SwitchToDialogue() { }
            public void Dispose()          { }
        }

        private sealed class FakePublisher<T> : IPublisher<T>
        {
            public void Publish(T message) { }
        }
    }
}
