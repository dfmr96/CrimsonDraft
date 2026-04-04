using UnityEngine;
using System.Collections.Generic;
using System;

namespace HorrorEngine
{
    public class InteractionColliderDetector : InteractionDetector
    {
        private Collider mCollider;

        // --------------------------------------------------------------------

        protected override void Awake()
        {
            base.Awake();
            mCollider = GetComponent<Collider>();
        }

        // --------------------------------------------------------------------

        private void OnEnable()
        {
            mCollider.enabled = true;
        }

        // --------------------------------------------------------------------

        protected override void OnDisable()
        {
            base.OnDisable();
            mCollider.enabled = false;
        }

        // --------------------------------------------------------------------

        private void OnTriggerEnter(Collider other)
        {
            Interactive interactive = other.GetComponent<Interactive>();
            if (interactive && interactive.isActiveAndEnabled)
            {
                Debug.Assert(!m_Interactives.Contains(interactive), "Trying to re-add an interactive to the InteractionDetector list", gameObject);

                AddInteractive(interactive);
            }
        }

        // --------------------------------------------------------------------

        private void OnTriggerExit(Collider other)
        {
            RemoveInteractive(other.gameObject);
        }

       
    }
}