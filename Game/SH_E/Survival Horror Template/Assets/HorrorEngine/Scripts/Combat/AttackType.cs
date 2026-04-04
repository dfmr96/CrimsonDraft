using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    [System.Serializable]
    public class AttackEffectEntry
    {
        public AttackEffect Effect;
        [Range(0, 1)] public float Chance = 1;
    }

    [System.Serializable]
    public class AttackImpact
    {
        public float Damage;
        public DamageableType[] Damageable;
        public AttackFilter[] Filters;
        public AttackEffectEntry[] PreDamageEffects;
        public AttackEffectEntry[] PostDamageEffects;
    }

    [CreateAssetMenu(menuName = "Horror Engine/Combat/AttackType")]
    public class AttackType : ScriptableObject
    {
        public AttackImpact[] Impacts;

        [SerializeField, HideInInspector] Dictionary<DamageableType, AttackImpact> m_HashedImpacts = new Dictionary<DamageableType, AttackImpact>();

        private void OnEnable()
        {
            m_HashedImpacts.Clear();
            MapImpacts(m_HashedImpacts, Impacts);

            
            var attackAddOn = Resources.LoadAll<AttackTypeAddOn>("");
            foreach (var addOn in attackAddOn)
            {
                if (addOn.AddsToAttack == this)
                {
                    MapImpact(m_HashedImpacts, addOn.Impact);
                }
            }
        }

        private void MapImpacts(Dictionary<DamageableType, AttackImpact>  dict, AttackImpact[] impacts)
        {
            foreach (var impact in impacts)
            {
                MapImpact(dict, impact);
            }
        }

        private void MapImpact(Dictionary<DamageableType, AttackImpact> dict, AttackImpact impact)
        {
            foreach (var type in impact.Damageable)
            {
                if (type)
                {
                    Debug.Assert(!dict.ContainsKey(type), $"AttackType has a duplicated Damageable entry for {type.name}");
                    dict.Add(type, impact);
                }
            }
        }

        public AttackImpact GetImpact(DamageableType damageable)
        {
            if (m_HashedImpacts.ContainsKey(damageable))
            {
                return m_HashedImpacts[damageable];
            }
            else
            {
                return null;
            }
        }
    }
}