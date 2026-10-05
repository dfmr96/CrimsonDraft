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
        public int      operatorSlot;  // which operator receives this item (ignored for storageItems)
    }

    [CreateAssetMenu(fileName = "StartingLoadout", menuName = "CrimsonDraft/Starting Loadout")]
    public sealed class StartingLoadout : ScriptableObject
    {
        [SerializeField] private OperatorData?[]     operatorSlots  = new OperatorData?[4];
        [SerializeField] private StartingItemEntry[] items          = Array.Empty<StartingItemEntry>();
        [SerializeField] private WeaponData?[]       defaultWeapons = new WeaponData?[4];
        [SerializeField] private MeleeWeaponData?[]  defaultMelee   = new MeleeWeaponData?[4];
        [SerializeField] private StartingItemEntry[] storageItems   = Array.Empty<StartingItemEntry>();

        public OperatorData?[]     OperatorSlots  => this.operatorSlots;
        public StartingItemEntry[] Items          => this.items;
        public WeaponData?[]       DefaultWeapons => this.defaultWeapons;
        public MeleeWeaponData?[]  DefaultMelee   => this.defaultMelee;
        public StartingItemEntry[] StorageItems   => this.storageItems;
    }
}
