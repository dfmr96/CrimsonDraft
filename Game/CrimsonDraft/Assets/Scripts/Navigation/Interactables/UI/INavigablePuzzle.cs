#nullable enable

namespace CrimsonDraft.Navigation.Interactables.UI
{
    public interface INavigablePuzzle
    {
        System.Action? OnSolved { get; set; }
        void MoveLeft();
        void MoveRight();
        void MoveUp();
        void MoveDown();
        void Toggle();
    }
}
