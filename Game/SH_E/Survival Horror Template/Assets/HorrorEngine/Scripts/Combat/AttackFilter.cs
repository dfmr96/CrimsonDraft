using UnityEngine;

namespace HorrorEngine
{
    public class AttackFilter : ScriptableObject
    {
        public virtual bool Passes(AttackInfo info) { return false; }
    }
}