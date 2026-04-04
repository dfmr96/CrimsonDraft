using System;
using UnityEngine;

namespace HorrorEngine
{
    public abstract class PlayerStateRotationOverride : MonoBehaviour
    {
        public ActorState[] States;

        public virtual void GetRotation(PlayerMovement movement, out float sign, out float rate)
        {
            sign = 1;
            rate = 0;
        }
    }
}
