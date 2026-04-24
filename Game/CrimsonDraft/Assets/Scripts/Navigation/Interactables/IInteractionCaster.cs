#nullable enable

using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Interactables
{
    public interface IInteractionCaster
    {
        bool CanUseItem(ItemData item);
        bool TryUseItem(ItemData item);
    }
}
