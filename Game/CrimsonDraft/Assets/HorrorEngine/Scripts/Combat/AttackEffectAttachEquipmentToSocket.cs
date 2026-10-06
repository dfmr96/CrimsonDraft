using System;
using UnityEngine;

namespace HorrorEngine
{
   
    [CreateAssetMenu(menuName = "Horror Engine/Combat/Effects/Attach Equipment To Socket")]
    public class AttackEffectAttachEquipmentToSocket : AttackEffect
    {
        [SerializeField] private EquipmentSlot m_Slot;
        [SerializeField] private SocketHandle m_Socket;

        public override void Apply(AttackInfo info)
        {
            base.Apply(info);

            var equipment = info.Attack.GetComponentInParent<PlayerEquipment>();
            equipment.GetEquipped(m_Slot, out ItemData item, out GameObject go);
            equipment.Unequip(m_Slot, false);

            SocketController socketCtrl = info.Damageable.GetComponentInParent<SocketController>();
            var socket = socketCtrl.GetSocket(m_Socket);

            go.transform.SetParent(socket.transform);
        }

    }
}