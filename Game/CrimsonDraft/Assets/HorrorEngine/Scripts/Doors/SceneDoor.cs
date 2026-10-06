using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    public abstract class DoorBase : MonoBehaviour
    {
        public DoorAnimation Animation;
        public Transform ExitPoint;
        public UnityEvent OnLocked;
        public UnityEvent OnOpened;
        
        protected Transform m_Interactor;

        public abstract void Use(IInteractor interactor);

        public abstract bool IsLocked();

        public void PlayerUse()
        {
            var player = GameManager.Instance.Player;
            if (player && player.TryGetComponent(out PlayerInteractor interactor))
            {
                Use(interactor);
            }
        }

        protected void TeleportInteractor(Transform point)
        {
            var actor = m_Interactor.GetComponent<Actor>();
            if (actor)
            {
                actor.PlaceAt(point);
            }
            else
            {
                m_Interactor.rotation = point.rotation;
                m_Interactor.position = point.position;
            }
        }

        protected virtual void OnDrawGizmosSelected()
        {
            if (ExitPoint)
            {
                Gizmos.DrawWireCube(ExitPoint.position, new Vector3(0.5f, 0f, 0.5f));
                GizmoUtils.DrawArrow(ExitPoint.position, ExitPoint.forward, 1f);
            }
        }

    }

    public class SceneDoor : DoorBase
    {
        public SceneDoor Exit;

        private DoorLock m_Lock;

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_Lock = GetComponent<DoorLock>();
        }

        // --------------------------------------------------------------------

        public override bool IsLocked()
        {
            return (m_Lock && m_Lock.IsLocked) || (Exit && Exit.m_Lock && Exit.m_Lock.IsLocked);
        }

        // --------------------------------------------------------------------

        public override void Use(IInteractor interactor)
        {
            if (m_Lock && m_Lock.IsLocked)
            {
                m_Lock.OnTryToUnlock(out bool open);
                if (!open)
                {
                    if (IsLocked())
                        OnLocked?.Invoke();
                    return;
                }
            }

            if (Exit && Exit.m_Lock && Exit.m_Lock.IsLocked)
            {
                Exit.m_Lock.OnTryToUnlockFromExit(out bool open);
                if (!open)
                {
                    if (IsLocked())
                        OnLocked?.Invoke();
                    return;
                }
            }

            OnOpened?.Invoke();
            MonoBehaviour interactorMB = (MonoBehaviour)interactor;
            m_Interactor = interactorMB.transform;
            DoorTransitionController.Instance.Trigger(this, interactorMB.gameObject, TransitionRoutine);
        }

        // --------------------------------------------------------------------

        private IEnumerator TransitionRoutine()
        {
            // Player teleportation
            TeleportInteractor(Exit.ExitPoint);

            yield return null;
        }
        

        // --------------------------------------------------------------------

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            if (Exit)
            {
                Gizmos.color = Exit.Exit == this ? Color.green : Color.red;
                Gizmos.DrawLine(transform.position, Exit.transform.position);
            }
        }
    }
}