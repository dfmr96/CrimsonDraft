using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    public enum AttackEffectInstantiateLocation
    {
        ImpactPoint,
        Damageable,
        Attack
    }

    [CreateAssetMenu(menuName = "Horror Engine/Combat/Effects/Instantiate")]
    public class AttackEffectInstantiate : AttackEffect       
    {
        public GameObject Prefab;
        public AttackEffectInstantiateLocation InstantiateAt;
        public ObjectInstantiationSettings Settings;

        public override void Apply(AttackInfo info)
        {
            base.Apply(info);

            Debug.Assert(Settings.Parent == null, "Parent can only be set dynamically by the AttackEffect", this);

            SocketController socketCtrl = null;
            switch (InstantiateAt)
            {
                case AttackEffectInstantiateLocation.ImpactPoint:
                    Settings.Position = info.ImpactPoint;
                    Debug.Assert(Settings.Socket == null, "Socket can't be used with AttackEffectInstantiateLocation.ImpactPoint", this);
                    break;
                case AttackEffectInstantiateLocation.Damageable:
                    Settings.Parent = info.Damageable.transform;
                    socketCtrl = info.Damageable.GetComponentInParent<SocketController>();
                    break;
                case AttackEffectInstantiateLocation.Attack :
                    Settings.Parent = info.Attack.transform;
                    socketCtrl = info.Attack.GetComponentInParent<SocketController>();
                    break;
            }

            Settings.Instantiate(Prefab, socketCtrl);
        }
    }
}