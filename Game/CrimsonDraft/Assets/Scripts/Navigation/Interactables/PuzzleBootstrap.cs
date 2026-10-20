#nullable enable

using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class PuzzleBootstrap : IInitializable
    {
        private readonly PuzzleStateRegistry registry;
        private readonly IPuzzle[]           puzzles;

        [Preserve]
        public PuzzleBootstrap(PuzzleStateRegistry registry, IPuzzle[] puzzles)
        {
            this.registry = registry;
            this.puzzles  = puzzles;
        }

        void IInitializable.Initialize()
        {
            foreach (var puzzle in this.puzzles)
                puzzle.BindPuzzleState(this.registry);
        }
    }
}
