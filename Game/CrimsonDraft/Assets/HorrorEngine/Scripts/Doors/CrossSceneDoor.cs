using System.Collections;
using UnityEngine;

namespace HorrorEngine
{

    public class CrossSceneTransitionMessage : BaseMessage
    {
        public CrossSceneDoor Door; // Door that triggered the transition
        public CrossSceneDoor ExitDoor; // Exit door located in the target scene
    }

    [RequireComponent(typeof(SceneTransition))]
    public class CrossSceneDoor : DoorBase
    {
        [SerializeField] private string m_DoorUniqueId;
        [SerializeField] private string m_ExitDoorUniqueId;
        
        private DoorLock m_Lock;
        private SceneTransition m_SceneTransition;

        CrossSceneTransitionMessage m_CrossSceneTransitionMessage = new CrossSceneTransitionMessage();


        // --------------------------------------------------------------------

        private void Awake()
        {
            m_SceneTransition = GetComponent<SceneTransition>();
            m_Lock = GetComponent<DoorLock>();
        }

        // --------------------------------------------------------------------

        public override bool IsLocked()
        {
            return (m_Lock && m_Lock.IsLocked);
        }

        // --------------------------------------------------------------------

        public override void Use(IInteractor interactor)
        {
            if (m_Lock && m_Lock.IsLocked)
            {
                m_Lock.OnTryToUnlock(out bool open);
                if (!open)
                {
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
            yield return m_SceneTransition.StartSceneTransition();

            bool doorFound = false;
            CrossSceneDoor[] doors = FindObjectsByType<CrossSceneDoor>(FindObjectsSortMode.None);
            foreach(var door in doors)
            {
                if (door.m_DoorUniqueId == m_ExitDoorUniqueId)
                {
                    m_CrossSceneTransitionMessage.Door = this;
                    m_CrossSceneTransitionMessage.ExitDoor = door;
                    TeleportInteractor(door.ExitPoint);
                    MessageBuffer<CrossSceneTransitionMessage>.Dispatch(m_CrossSceneTransitionMessage);
                    doorFound = true;
                    break;
                }
            }

            Debug.Assert(doorFound, $"CrossScene door exit with Id: {m_ExitDoorUniqueId} not found from {doors.Length} candidates");

            yield return Yielders.UnscaledTime(1.0f);
        }

    }
}