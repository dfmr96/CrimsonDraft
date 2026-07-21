#nullable enable

using UnityEngine;

namespace CrimsonDraft.Operators
{
    public sealed class OperatorRuntime
    {
        public OperatorData?  Data           { get; }
        public int            SlotIndex      { get; }
        public bool           IsPresent      { get; }
        public int            MaxHp          { get; }

        public int            Hp              { get; private set; }
        public IWeaponSlot?   PrimaryWeapon   { get; private set; }
        public IWeaponSlot?   SecondaryWeapon { get; private set; }
        public IWeaponSlot?   ActiveWeapon    => this.PrimaryWeapon ?? this.SecondaryWeapon;
        public float          HpRatio         => this.MaxHp > 0 ? Mathf.Clamp01((float)this.Hp / this.MaxHp) : 0f;
        public bool           IsAlive        => this.IsPresent && this.Hp > 0;

        internal OperatorRuntime(int slotIndex, OperatorData? data, bool isPresent, int maxHp)
        {
            this.SlotIndex = slotIndex;
            this.Data      = data;
            this.IsPresent = isPresent;
            this.MaxHp     = maxHp;
            this.Hp        = isPresent ? maxHp : 0;
        }

        public void Heal(int amount)
            => this.Hp = UnityEngine.Mathf.Clamp(this.Hp + UnityEngine.Mathf.Max(0, amount), 0, this.MaxHp);

        internal void RestoreHp(int hp)
            => this.Hp = UnityEngine.Mathf.Clamp(hp, 0, this.MaxHp);

        public OperatorDamageResult ApplyDamage(int damage)
        {
            if (!this.IsAlive)
                return new OperatorDamageResult(this.SlotIndex, 0, this.Hp, true);

            int applied = Mathf.Max(0, damage);
            this.Hp = Mathf.Max(0, this.Hp - applied);
            return new OperatorDamageResult(this.SlotIndex, applied, this.Hp, this.Hp <= 0);
        }

        public void SetEquippedWeapon(IWeaponSlot? weapon, int slotIndex = 0)
        {
            if (slotIndex == 0) this.PrimaryWeapon   = weapon;
            else                this.SecondaryWeapon = weapon;
        }
    }
}
