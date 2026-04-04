using UnityEngine;

namespace HorrorEngine
{
    public class CompleteMapRoomStepOnContainerEmpty : MonoBehaviour
    {
        private LocalItemContainer m_Container;

        private void Awake()
        {
            m_Container = GetComponent<LocalItemContainer>();
            m_Container.OnClose.AddListener(OnContainerClose);
        }

        private void OnContainerClose()
        {
            GetComponent<MapRoomCompletionStep>().SetCompleted(m_Container.IsEmpty());
        }
    }
}