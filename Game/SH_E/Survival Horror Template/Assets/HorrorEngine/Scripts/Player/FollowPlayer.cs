using UnityEngine;

namespace HorrorEngine
{
    public class FollowPlayer : MonoBehaviour
    {
        [SerializeField] Vector3 LocalOffset;

        private void Update()
        {
            if (GameManager.Instance.Player) 
            {
                Transform playerT = GameManager.Instance.Player.transform;
                transform.position = playerT.TransformPoint(LocalOffset);   
            }
        }
    }
}