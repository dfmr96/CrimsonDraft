#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Navigation.Dialogue;
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Navigation.Interactables.UI;

namespace CrimsonDraft.Tests
{
    public sealed class PuzzleInteractableTests
    {
        // ── Fakes ──────────────────────────────────────────────────────────────

        private sealed class FakeDialogueService : IDialogueService
        {
            public bool IsRunning => false;
            public string? LastNodeName { get; private set; }

            public void StartDialogue(
                string nodeName,
                IReadOnlyDictionary<string, object>? variables = null,
                Action? onComplete = null,
                IReadOnlyDictionary<string, Action>? commands = null)
            {
                this.LastNodeName = nodeName;
            }

            public void SetVariable(string name, object value) { }
        }

        private sealed class FakeInputService : IInputService
        {
            public InputAction Move                   => null!;
            public InputAction Interact               => null!;
            public InputAction OpenInventory          => null!;
            public InputAction OpenMap                => null!;
            public InputAction Aim                    => null!;
            public InputAction AimFire                => null!;
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
            public InputAction PickupNavigate         => null!;
            public InputAction PickupConfirm          => null!;
            public InputAction InventoryNavigate      => null!;
            public InputAction InventoryConfirm       => null!;
            public InputAction InventoryPickup        => null!;
            public InputAction InventoryCancel        => null!;
            public InputAction InventoryNextTab       => null!;
            public InputAction InventoryPrevTab       => null!;
            public InputAction InventoryCloseMap      => null!;
            public InputAction InventoryClose         => null!;
            public void SwitchToGameplay()       { }
            public void SwitchToCombat()         { }
            public void SwitchToUI()             { }
            public void SwitchToDialogue()       { }
            public void SwitchToDoorTransition() { }
            public void SwitchToPickupPrompt()   { }
            public void SwitchToInventory()      { }
            public void Dispose()                { }
        }

        private sealed class FakeNavigablePuzzle : MonoBehaviour, INavigablePuzzle
        {
            public Action? OnSolved { get; set; }
            public void MoveLeft()  { }
            public void MoveRight() { }
            public void MoveUp()    { }
            public void MoveDown()  { }
            public void Toggle()    { }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private readonly List<GameObject> spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in this.spawned)
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            this.spawned.Clear();
            Time.timeScale = 1f;
        }

        private GameObject Track(GameObject go)
        {
            this.spawned.Add(go);
            return go;
        }

        private static InteractionContext MakeContext(FakeDialogueService dialogue, PuzzleViewController puzzleView) =>
            new InteractionContext(null!, null!, dialogue, null!, null!, null!, puzzleView, null!);

        private PuzzleInteractable MakePuzzle(GameObject canvasPrefab, ItemSocketInteractable? socket = null)
        {
            var go     = Track(new GameObject());
            var puzzle = go.AddComponent<PuzzleInteractable>();
            var so     = new UnityEditor.SerializedObject(puzzle);
            so.FindProperty("canvasPrefab").objectReferenceValue   = canvasPrefab;
            so.FindProperty("requiredSocket").objectReferenceValue = socket;
            so.ApplyModifiedPropertiesWithoutUndo();
            return puzzle;
        }

        // Instantiating the canvas prefab in an EditMode test also finds the
        // never-destroyed template GameObject via FindObjectsOfType, so we
        // diff against a snapshot taken before Interact() to isolate what was
        // newly spawned by PuzzleViewController.Open().
        private static List<FakeNavigablePuzzle> NewlySpawned(IEnumerable<FakeNavigablePuzzle> before, Action act)
        {
            var beforeSet = new HashSet<FakeNavigablePuzzle>(before);
            act();
            return UnityEngine.Object.FindObjectsOfType<FakeNavigablePuzzle>()
                .Where(p => !beforeSet.Contains(p))
                .ToList();
        }

        // ── Tests ─────────────────────────────────────────────────────────────

        [Test]
        public void Interact_withUnactivatedRequiredSocket_delegatesToSocket_doesNotOpenPuzzle()
        {
            var canvasPrefab = Track(new GameObject("CanvasPrefab"));
            var socketGo     = Track(new GameObject("Socket"));
            var socket       = socketGo.AddComponent<ItemSocketInteractable>();
            var puzzle       = MakePuzzle(canvasPrefab, socket);

            var puzzleView = new PuzzleViewController(new FakeInputService());
            var dialogue   = new FakeDialogueService();

            puzzle.Interact(MakeContext(dialogue, puzzleView));

            Assert.IsNotNull(dialogue.LastNodeName, "socket.Interact should have started dialogue");
            Assert.AreEqual(1f, Time.timeScale, "puzzle canvas should not have opened, so the game stays unpaused");
        }

        [Test]
        public void Interact_withNoRequiredSocket_opensPuzzleCanvas()
        {
            var canvasPrefab = Track(new GameObject("CanvasPrefab"));
            canvasPrefab.AddComponent<FakeNavigablePuzzle>();
            var puzzle = MakePuzzle(canvasPrefab);

            var puzzleView = new PuzzleViewController(new FakeInputService());
            var dialogue   = new FakeDialogueService();

            var before  = UnityEngine.Object.FindObjectsOfType<FakeNavigablePuzzle>();
            var newOnes = NewlySpawned(before, () => puzzle.Interact(MakeContext(dialogue, puzzleView)));

            Assert.AreEqual(0f, Time.timeScale, "opening the puzzle canvas should pause the game");
            Assert.AreEqual(1, newOnes.Count, "canvas prefab should have been instantiated exactly once");
            foreach (var p in newOnes) Track(p.gameObject);
        }

        [Test]
        public void Interact_afterSolved_doesNotReopenPuzzle()
        {
            var canvasPrefab = Track(new GameObject("CanvasPrefab"));
            canvasPrefab.AddComponent<FakeNavigablePuzzle>();
            var puzzle = MakePuzzle(canvasPrefab);

            var puzzleView = new PuzzleViewController(new FakeInputService());
            var dialogue   = new FakeDialogueService();

            var before      = UnityEngine.Object.FindObjectsOfType<FakeNavigablePuzzle>();
            var firstOpened = NewlySpawned(before, () => puzzle.Interact(MakeContext(dialogue, puzzleView)));
            Assert.AreEqual(1, firstOpened.Count);
            foreach (var p in firstOpened) Track(p.gameObject);

            // PuzzleViewController.Close() calls Object.Destroy, which is only
            // valid in Play mode; Unity logs an error when it runs in EditMode.
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Destroy may not be called from edit mode.*"));
            firstOpened[0].OnSolved?.Invoke();

            var afterSolve  = UnityEngine.Object.FindObjectsOfType<FakeNavigablePuzzle>();
            var reopened    = NewlySpawned(afterSolve, () => puzzle.Interact(MakeContext(dialogue, puzzleView)));

            Assert.AreEqual(0, reopened.Count, "already-solved puzzle should not be reopened");
        }
    }
}
