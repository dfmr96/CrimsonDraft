using UnityEngine;

namespace HorrorEngine
{
    public class SenseTargetPlayer : SenseTarget
    {
        public override Transform GetTransform()
        {
            if (GameManager.Exists && GameManager.Instance.Player)
                return GameManager.Instance.Player.transform;
            else
                return null;
        }
    }
}