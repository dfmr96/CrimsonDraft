#nullable enable

using UnityEngine;

namespace CrimsonDraft.Operators
{
    public sealed class OperatorRuntime
    {
        public OperatorData? Data      { get; }
        public int                SlotIndex { get; }
        public bool               IsPresent { get; }
        public int                MaxHp     { get; }
        public int                MaxAmmo   { get; }

        public int   Hp      { get; private set; }
        public int   Ammo    { get; private set; }
        public float HpRatio => this.MaxHp > 0 ? Mathf.Clamp01((float)this.Hp / this.MaxHp) : 0f;
        public bool  IsAlive  => this.IsPresent && this.Hp > 0;

        internal OperatorRuntime(int slotIndex, OperatorData? data, bool isPresent, int maxHp, int maxAmmo)
        {
            this.SlotIndex = slotIndex;
            this.Data      = data;
            this.IsPresent = isPresent;
            this.MaxHp     = maxHp;
            this.MaxAmmo   = maxAmmo;
            this.Hp        = isPresent ? maxHp  : 0;
            this.Ammo      = isPresent ? maxAmmo : 0;
        }

        public OperatorDamageResult ApplyDamage(int damage)
        {
            if (!this.IsAlive)
                return new OperatorDamageResult(this.SlotIndex, 0, this.Hp, true);

            int applied = Mathf.Max(0, damage);
            this.Hp = Mathf.Max(0, this.Hp - applied);
            return new OperatorDamageResult(this.SlotIndex, applied, this.Hp, this.Hp <= 0);
        }

        public void Reload()              => this.Ammo = this.IsPresent ? this.MaxAmmo : 0;
        public void ConsumeAmmo(int shots) => this.Ammo = Mathf.Max(0, this.Ammo - Mathf.Max(0, shots));
    }
}
