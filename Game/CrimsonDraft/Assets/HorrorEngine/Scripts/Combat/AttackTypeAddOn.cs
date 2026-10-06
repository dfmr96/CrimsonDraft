using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    [CreateAssetMenu(menuName = "Horror Engine/Combat/AttackType AddOn")]
    public class AttackTypeAddOn : ScriptableObject
    {
        public AttackType AddsToAttack;
        public AttackImpact Impact;
    }
}
