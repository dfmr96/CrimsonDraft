#nullable enable

using System;
using UnityEngine;

namespace CrimsonDraft.Inventory
{
    [Serializable]
    public struct CombineRecipe
    {
        [SerializeField] private ItemData inputA;
        [SerializeField] private ItemData inputB;
        [SerializeField] private ItemData output;

        public ItemData InputA => this.inputA;
        public ItemData InputB => this.inputB;
        public ItemData Output => this.output;
    }
}
