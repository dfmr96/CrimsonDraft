#nullable enable

namespace CrimsonDraft.Inventory
{
    public interface ICombineService
    {
        /// <summary>Returns the output ItemData if a recipe exists for (a, b). Symmetric — order does not matter. Returns null if no recipe.</summary>
        ItemData? TryGetResult(ItemData a, ItemData b);

        /// <summary>Returns true if the item participates in at least one recipe.</summary>
        bool HasAnyRecipe(ItemData item);
    }
}
