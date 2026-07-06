#nullable enable

namespace CrimsonDraft.Navigation.Interactables
{
    /// <summary>
    /// Optional companion to IInteractable. Implement (or attach a component implementing)
    /// this on interactables that need a non-default Interact animation.
    /// </summary>
    public interface IAnimatedInteractable
    {
        InteractionAnimType AnimType { get; }
    }
}
