#nullable enable

using System;
using UnityEngine;
using CrimsonDraft.Operators;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation
{
    [Serializable]
    public struct StartingItemEntry
    {
        public ItemData item;
        public int      quantity;
        public int      operatorSlot;  // which operator's 4-slot section this item goes to
    }

    [CreateAssetMenu(fileName = "StartingLoadout", menuName = "CrimsonDraft/Starting Loadout")]
    public sealed class StartingLoadout : ScriptableObject
    {
        [SerializeField] private OperatorData?[]     operatorSlots  = new OperatorData?[4];
        [SerializeField] private StartingItemEntry[] items          = Array.Empty<StartingItemEntry>();
        [SerializeField] private WeaponData?[]       defaultWeapons = new WeaponData?[4];

        public OperatorData?[]     OperatorSlots  => this.operatorSlots;
        public StartingItemEntry[] Items          => this.items;
        public WeaponData?[]       DefaultWeapons => this.defaultWeapons;
    }
}
