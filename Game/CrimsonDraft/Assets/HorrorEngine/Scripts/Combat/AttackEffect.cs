using UnityEngine;

namespace HorrorEngine
{
    public class AttackEffect : ScriptableObject
    {
        public virtual void Apply(AttackInfo info) { }
    }
}