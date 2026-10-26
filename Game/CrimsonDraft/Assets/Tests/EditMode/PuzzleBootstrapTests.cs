#nullable enable

using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.Tests
{
    public sealed class PuzzleBootstrapTests
    {
        private sealed class FakePuzzle : IPuzzle
        {
            public string PuzzleId => "fake";
            public string GateId => "puzzle:fake";
            public PuzzleStateRegistry? Bound;
            public void BindPuzzleState(PuzzleStateRegistry registry) => this.Bound = registry;
        }

        [Test]
        public void Initialize_bindsEveryPuzzle()
        {
            var registry = new PuzzleStateRegistry();
            var a = new FakePuzzle();
            var b = new FakePuzzle();

            ((IInitializable)new PuzzleBootstrap(registry, new IPuzzle[] { a, b })).Initialize();

            Assert.AreSame(registry, a.Bound);
            Assert.AreSame(registry, b.Bound);
        }

        // Final review I1: a generator solved in a save from before PuzzleStateRegistry existed is
        // restored from its own "#solved" key; it must report itself solved so its milestone completes.
        [Test]
        public void GeneratorSwitchPanel_restoredAsSolved_reportsToPuzzleRegistry()
        {
            var go = new GameObject("Generator", typeof(GeneratorSwitchPanel));
            try
            {
                var panel   = go.GetComponent<GeneratorSwitchPanel>();
                var sockets = new ItemSocketStateRegistry();
                sockets.SetInserted(panel.PuzzleId + "#solved", new[] { true });
                panel.Construct(null!, null!, null!, sockets, null!);
                var puzzles = new PuzzleStateRegistry();
                ((IPuzzle)panel).BindPuzzleState(puzzles);

                typeof(GeneratorSwitchPanel)
                    .GetMethod("Start", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(panel, null);

                Assert.IsTrue(panel.IsSolved);
                Assert.IsTrue(puzzles.IsSolved(panel.PuzzleId));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void PuzzleInteractable_solvingCallsSetSolved()
        {
            var go = new GameObject("Puzzle", typeof(PuzzleInteractable));
            try
            {
                var puzzle = go.GetComponent<PuzzleInteractable>();
                var so = new SerializedObject(puzzle);
                so.FindProperty("puzzleId").stringValue = "lab_terminal";
                so.ApplyModifiedPropertiesWithoutUndo();
                var registry = new PuzzleStateRegistry();
                ((IPuzzle)puzzle).BindPuzzleState(registry);

                puzzle.MarkSolved();

                Assert.IsTrue(registry.IsSolved("lab_terminal"));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
