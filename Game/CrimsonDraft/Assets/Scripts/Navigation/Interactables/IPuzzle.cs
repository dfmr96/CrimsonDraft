#nullable enable

using CrimsonDraft.Infrastructure;
using CrimsonDraft.Navigation.Gates;

namespace CrimsonDraft.Navigation.Interactables
{
    // A puzzle that reports "solved" to the shared PuzzleStateRegistry. It is a gate: solved means
    // completed, so milestones and camera zones can wait on it (GateIds.Puzzle). Bound once per scene load by
    // PuzzleBootstrap from NavigationScope's cached puzzles.
    public interface IPuzzle : IGate
    {
        string PuzzleId { get; }
        void BindPuzzleState(PuzzleStateRegistry registry);
    }
}
