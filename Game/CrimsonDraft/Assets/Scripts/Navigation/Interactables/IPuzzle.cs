#nullable enable

using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Navigation.Interactables
{
    // A puzzle that reports "solved" to the shared PuzzleStateRegistry, so milestones can
    // trigger on it (MilestoneTriggerKind.PuzzleSolved). Bound once per scene load by
    // PuzzleBootstrap from NavigationScope's cached puzzles.
    public interface IPuzzle
    {
        string PuzzleId { get; }
        void BindPuzzleState(PuzzleStateRegistry registry);
    }
}
