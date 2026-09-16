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

        public int                Hp            { get; private set; }
        public IWeaponSlot?       PrimaryWeapon { get; private set; }
        public IWeaponSlot?       ActiveWeapon  => this.PrimaryWeapon;
        public IMeleeWeapon?      MeleeWeapon   { get; private set; }
        public float              HpRatio       => this.MaxHp > 0 ? Mathf.Clamp01((float)this.Hp / this.MaxHp) : 0f;

        // Reaching 0 HP no longer means dead outright -- it means Mercy (see IsMercy, named
        // after RE2's mercy-invincibility rule): one more hit is needed to actually finish
        // them off (IsDead). IsAlive stays true through Mercy so the operator can still act,
        // be targeted, and be healed back from the brink; only a confirmed kill (KIA) flips
        // it false.
        private bool          isDead;
        public bool           IsAlive        => this.IsPresent && !this.isDead;
        public bool           IsMercy        => this.IsAlive && this.Hp <= 0;

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

        // Save/load and roster seeding only ever cross HP outside of combat, where Mercy
        // can't persist (a Mercy survivor is healed to 1 HP the moment combat ends -- see
        // CombatOrchestrator.ReviveMercyOperatorsOnCombatEnd) -- so 0 HP here always means a
        // confirmed KIA, same as the old Hp<=0-is-dead rule.
        internal void RestoreHp(int hp)
        {
            this.Hp     = UnityEngine.Mathf.Clamp(hp, 0, this.MaxHp);
            this.isDead = this.Hp <= 0;
        }

        public OperatorDamageResult ApplyDamage(int damage)
        {
            if (!this.IsAlive)
                return new OperatorDamageResult(this.SlotIndex, 0, this.Hp, true);

            int applied = Mathf.Max(0, damage);

            // Already at death's door -- this hit is the one that finishes them off.
            if (this.IsMercy)
            {
                this.isDead = true;
                return new OperatorDamageResult(this.SlotIndex, applied, this.Hp, true);
            }

            this.Hp = Mathf.Max(0, this.Hp - applied);
            return new OperatorDamageResult(this.SlotIndex, applied, this.Hp, false);
        }

        public void SetEquippedWeapon(IWeaponSlot? weapon, int slotIndex = 0)
        {
            this.PrimaryWeapon = weapon;
        }

        // Set once at run bootstrap from the operator's starting loadout -- the melee weapon
        // is permanently equipped and never swapped through the inventory/equip pipeline.
        public void SetMeleeWeapon(IMeleeWeapon? melee)
        {
            this.MeleeWeapon = melee;
        }
    }
}
