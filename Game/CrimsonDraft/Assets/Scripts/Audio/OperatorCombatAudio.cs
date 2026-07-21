#nullable enable

using System;
using UnityEngine;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Audio
{
    public sealed class OperatorCombatAudio : MonoBehaviour
    {
        [Serializable]
        public struct GunTypeSwitchEntry
        {
            public GunType         GunType;
            public AK.Wwise.Switch WwiseSwitch;
        }

        [SerializeField] private AK.Wwise.Event       fireGunEvent     = new();
        [SerializeField] private GunTypeSwitchEntry[] gunTypeSwitches  = Array.Empty<GunTypeSwitchEntry>();
        [SerializeField] private AK.Wwise.Event       shellCasingEvent = new();

        private IOperatorRoster? roster;
        private int              slotIndex = -1;

        public void Bind(IOperatorRoster roster, int slotIndex)
        {
            this.roster    = roster;
            this.slotIndex = slotIndex;
        }

        // Called by Animation Event on the ShootPistolFlexed2 clip.
        public void PlayFireGunSfx()
        {
            if (this.roster == null || this.slotIndex < 0 || this.roster.Count <= this.slotIndex)
                return;

            var weapon = this.roster[this.slotIndex].ActiveWeapon;
            if (weapon == null)
                return;

            foreach (var entry in this.gunTypeSwitches)
            {
                if (entry.GunType == weapon.GunType)
                {
                    entry.WwiseSwitch.SetValue(gameObject);
                    break;
                }
            }

            this.fireGunEvent.Post(gameObject);
        }

        // Called by Animation Event on the ShootPistolFlexed2 clip, later than PlayFireGunSfx's event.
        public void PlayShellCasingSfx() => this.shellCasingEvent.Post(gameObject);
    }
}
