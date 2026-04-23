#nullable enable

using System;
using System.Collections.Generic;
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

        void IInitializable.Initialize()
        {
            this.lookup.Clear();
            foreach (var recipe in this.library.Recipes)
            {
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
