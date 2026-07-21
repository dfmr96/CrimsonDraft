#nullable enable

namespace CrimsonDraft.Navigation.Interactables
{
    public enum InteractionAnimType
    {
        Stand  = 0,
        Stand2 = 1,
        Crouch = 2,
        // Plays no Interact animation at all (e.g. doors, which have their own transition).
        None   = 3
    }

    public static class InteractionAnimTypeExtensions
    {
        /// <summary>Maps to the IntType blend tree threshold (0 / 0.5 / 1) on the Interact state.</summary>
        public static float ToBlendThreshold(this InteractionAnimType type) => type switch
        {
            InteractionAnimType.Stand  => 0f,
            InteractionAnimType.Stand2 => 0.5f,
            InteractionAnimType.Crouch => 1f,
            _                          => 0f
        };
    }
}
