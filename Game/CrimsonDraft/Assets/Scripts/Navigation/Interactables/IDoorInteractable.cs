#nullable enable

namespace CrimsonDraft.Navigation.Interactables
{
    public interface IDoorInteractable
    {
        string DoorId { get; }
        void   RestoreFromRegistry();
    }
}
