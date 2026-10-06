using UnityEngine;

namespace HorrorEngine
{
    [RequireComponent(typeof(Interactive))]
    public class Climbable : MonoBehaviour
    {
        public ClimbableSetup Setup;

        public Transform ExitTop;
        public Transform ExitBottom;

        private Interactive m_Interactive;

        private void Awake()
        {
            m_Interactive = GetComponent<Interactive>();
            m_Interactive.OnInteract.AddListener((interactor) =>
            {
                var playerInteractor = interactor as PlayerInteractor;
                if (playerInteractor)
                {
                    Actor playerActor = playerInteractor.GetComponentInParent<Actor>();
                    PlayerStateClimbing climbingState = playerActor.GetComponentInChildren<PlayerStateClimbing>();
                    climbingState.StartClimb(this);
                }
            });
        }

        public Vector3 ClimbTop => new Vector3(transform.position.x, ExitTop.position.y, transform.position.z) + transform.forward * Setup.ClimbZOffset;
        public Vector3 ClimbBottom => new Vector3(transform.position.x, ExitBottom.position.y, transform.position.z) + transform.forward * Setup.ClimbZOffset;

        public Vector3 DropTop => new Vector3(transform.position.x, ExitTop.position.y, transform.position.z) + transform.forward * Setup.DropZOffset;
        public Vector3 DropBottom => new Vector3(transform.position.x, ExitBottom.position.y, transform.position.z) + transform.forward * Setup.DropZOffset;

        private void OnDrawGizmosSelected()
        {
            if (Setup)
            {
                Vector3 climbTop = ClimbTop;
                Gizmos.color = new Color(0, 1, 0, 0.5f);
                Gizmos.DrawLine(climbTop, ClimbBottom);
                Gizmos.DrawLine(climbTop - Vector3.up * Setup.ClimbExitDistance - transform.right * 0.25f, climbTop - Vector3.up * Setup.ClimbExitDistance + transform.right * 0.25f);


                Vector3 dropBottom = DropBottom;
                Gizmos.color = new Color(0, 1, 1, 0.5f);
                Gizmos.DrawLine(DropTop, dropBottom);
                Gizmos.DrawLine(dropBottom + Vector3.up * Setup.DropExitDistance - transform.right * 0.25f, dropBottom + Vector3.up * Setup.DropExitDistance + transform.right * 0.25f);

                Gizmos.color = Color.white;
                GizmoUtils.DrawArrow(DropTop, transform.TransformDirection(Setup.DropEntryDirection), 0.5f);
                GizmoUtils.DrawArrow(ClimbBottom, transform.TransformDirection(Setup.ClimbEntryDirection), 0.5f);
            }

            if (ExitTop)    
            {
                Gizmos.DrawWireCube(ExitTop.position, new Vector3(0.5f, 0f, 0.5f));
                GizmoUtils.DrawArrow(ExitTop.position, ExitTop.forward, 1f);
            }

            if (ExitBottom)
            {
                Gizmos.DrawWireCube(ExitBottom.position, new Vector3(0.5f, 0f, 0.5f));
                GizmoUtils.DrawArrow(ExitBottom.position, ExitBottom.forward, 1f);
            }

            
        }
    }
}