#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Inventory
{
    [CreateAssetMenu(menuName = "CrimsonDraft/Combine Recipe Library", fileName = "CombineRecipeLibrary")]
    public sealed class CombineRecipeLibrary : ScriptableObject
    {
        [SerializeField] private List<CombineRecipe> recipes = new();

        public IReadOnlyList<CombineRecipe> Recipes => this.recipes;
    }
}
