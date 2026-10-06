using UnityEngine;

namespace HorrorEngine
{
    public class InteractionRaycastDetector : InteractionDetector
    {
        [SerializeField] private LayerMask m_LayerMask;
        [SerializeField] private float m_Distance;

        // --------------------------------------------------------------------

        public void Cast()
        {
            if (Physics.Raycast(new Ray(transform.position, transform.forward), out RaycastHit hit, m_Distance, m_LayerMask, QueryTriggerInteraction.Collide))
            {
                Interactive interactive = hit.collider.GetComponent<Interactive>();
                if (interactive && interactive.isActiveAndEnabled)
                {   
                    if (FocusedInteractive == interactive)
                        return;

                    ClearAll(); // We only have 1 interactable in the list at all times for this detector
                    AddInteractive(interactive);
                }
                else // Something is obstructing the ray
                {
                    ClearAll();
                }
            }
            else
            {
                ClearAll();
            }
        }

        private void ClearAll()
        {
            while (m_Interactives.Count > 0)
                RemoveInteractive(m_Interactives[0]);
        }

        // --------------------------------------------------------------------

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, transform.position+ transform.forward * m_Distance);
        }
    }
}