#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure
{
    // One shared "solved" flag per puzzle (IPuzzle.PuzzleId), saved with the game.
    public sealed class PuzzleStateRegistry
    {
        private readonly HashSet<string> solved = new();

        [Preserve]
        public PuzzleStateRegistry() { }

        public event Action? Changed;

        public IReadOnlyCollection<string> SolvedIds => this.solved;

        public bool IsSolved(string puzzleId) =>
            !string.IsNullOrEmpty(puzzleId) && this.solved.Contains(puzzleId);

        public void SetSolved(string puzzleId)
        {
            if (string.IsNullOrEmpty(puzzleId) || !this.solved.Add(puzzleId)) return;
            Changed?.Invoke();
        }

        public void LoadState(IEnumerable<string> saved)
        {
            this.solved.Clear();
            foreach (var id in saved)
                if (!string.IsNullOrEmpty(id)) this.solved.Add(id);
        }

        public void ClearAll() => this.solved.Clear();
    }
}
