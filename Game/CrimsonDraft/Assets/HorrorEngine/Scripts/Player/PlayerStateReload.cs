using System;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    public class PlayerStateReload : ActorStateWithDuration
    {
        private static readonly string k_EventAmmoAttachToSocket = "AmmoAttachToSocket";
        private static readonly string k_EventAmmoAttachToWeapon = "AmmoAttachToWeapon";

        private AudioSource m_AudioSource;
        private InventoryEntry m_WeaponEntry;
        private ReloadableWeaponData m_Weapon;
        private UnityAction<AnimationEvent> m_OnAnimatorEvent;
        private PlayerEquipment m_Equipment;
        private WeaponAmmoVisuals m_WeaponAmmoVisuals;
        private SocketController m_SocketController;

        // --------------------------------------------------------------------

        protected override void Awake()
        {
            base.Awake();

            m_OnAnimatorEvent = OnAnimatorEvent;
            m_AudioSource = GetComponentInParent<AudioSource>();
            m_Equipment = GetComponentInParent<PlayerEquipment>();
            m_SocketController = GetComponentInParent<SocketController>();
        }

        // --------------------------------------------------------------------

        public override void StateEnter(IActorState fromState)
        {
            var weaponInstance = m_Equipment.GetWeaponInstance(GameManager.Instance.Inventory.WeaponSlot).GetComponent<Weapon>();
            if (weaponInstance)
                m_WeaponAmmoVisuals = weaponInstance.GetComponent<WeaponAmmoVisuals>();

            m_WeaponEntry = GameManager.Instance.Inventory.GetEquippedWeapon();
            m_Weapon = m_WeaponEntry.Item as ReloadableWeaponData;
            Debug.Assert(m_Weapon, "ReloadableWeapon not equipped, it is assumed it'll be equipped in the primary equipment slot");

            m_Duration = m_Weapon.ReloadDuration;

            base.StateEnter(fromState);

            m_AudioSource.PlayOneShot(m_Weapon.ReloadSound);

            UIManager.Get<UIInputListener>().AddBlockingContext(this);

            Actor.MainAnimator.GetComponent<AnimatorEventHandler>().OnEvent.AddListener(m_OnAnimatorEvent);
        }

        // --------------------------------------------------------------------

        public override void StateExit(IActorState intoState)
        {
            Reload();

            UIManager.Get<UIInputListener>().RemoveBlockingContext(this);
            Actor.MainAnimator.GetComponent<AnimatorEventHandler>().OnEvent.RemoveListener(m_OnAnimatorEvent);

            base.StateExit(intoState);
        }

        // --------------------------------------------------------------------

        public void OnAnimatorEvent(AnimationEvent e)
        {
            if (m_WeaponAmmoVisuals != null)
            {
                if (e.stringParameter == k_EventAmmoAttachToSocket)
                {
                    m_WeaponAmmoVisuals.AttachAmmoToSocket(m_SocketController);
                }
                else if (e.stringParameter == k_EventAmmoAttachToWeapon)
                {
                    m_WeaponAmmoVisuals.AttachAmmoToWeapon();
                }
            }
        }

        // --------------------------------------------------------------------

        public virtual void Reload()
        {
            Debug.Assert(m_Weapon.AmmoItem != null, "Weapon can not reload, AmmoItem is null in the WeaponData");

            Inventory inventory = GameManager.Instance.Inventory;
            if (inventory.TryGet(m_Weapon.AmmoItem, out var ammoEntry))
                inventory.Combine(m_WeaponEntry, ammoEntry);
        }

    }
}