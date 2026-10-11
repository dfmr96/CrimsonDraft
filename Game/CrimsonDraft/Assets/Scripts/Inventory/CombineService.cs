#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;
using VContainer.Unity;

namespace CrimsonDraft.Inventory
{
    public sealed class CombineService : ICombineService, IInitializable
    {
        private readonly CombineRecipeLibrary                   library;
        private readonly Dictionary<(string, string), ItemData> lookup = new();

        [Preserve]
        public CombineService(CombineRecipeLibrary library) => this.library = library;

        // A recipe pointing at a deleted item asset is skipped with a warning instead of throwing:
        // an exception here aborts the whole loop and leaves every recipe in the library unusable.
        void IInitializable.Initialize()
        {
            this.lookup.Clear();
            var recipes = this.library.Recipes;
            for (int i = 0; i < recipes.Count; i++)
            {
                var recipe = recipes[i];
                if (recipe.InputA == null || recipe.InputB == null || recipe.Output == null)
                {
                    Debug.LogWarning($"[CombineService] Recipe {i} in '{this.library.name}' references a missing item and was skipped.", this.library);
                    continue;
                }

                var key = MakeKey(recipe.InputA.ItemId, recipe.InputB.ItemId);
                this.lookup[key] = recipe.Output;
            }
        }

        public ItemData? TryGetResult(ItemData a, ItemData b)
        {
            var key = MakeKey(a.ItemId, b.ItemId);
            return this.lookup.TryGetValue(key, out var result) ? result : null;
        }

        private static (string, string) MakeKey(string idA, string idB) =>
            string.Compare(idA, idB, StringComparison.Ordinal) <= 0
                ? (idA, idB)
                : (idB, idA);
    }
}
