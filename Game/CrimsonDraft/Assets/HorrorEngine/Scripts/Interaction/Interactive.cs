using UnityEngine;
using System;
using UnityEngine.Events;

namespace HorrorEngine
{
    public class InteractionStartMessage : BaseMessage
    {
    }
    public class InteractionEndMessage : BaseMessage
    {
    }

    [Serializable]
    public class OnInteractionEvent : UnityEvent<IInteractor> {}

    [RequireComponent(typeof(OnDisableNotifier))]
    public class Interactive : MonoBehaviour
    {
        public InteractionData Data;
        public bool ShowPrompt = true;
        public OnInteractionEvent OnInteract;
        public UnityEvent OnFinishInteract;
        public UnityEvent OnDataChanged;

        // --------------------------------------------------------------------

        public void SetInteractable(bool active)
        {
            gameObject.SetActive(active);
        }

        // --------------------------------------------------------------------

        public void Interact(IInteractor interactor)
        {
            if (!isActiveAndEnabled)
                return;

            OnInteract?.Invoke(interactor);
            MessageBuffer<InteractionStartMessage>.Dispatch();
        }

        // --------------------------------------------------------------------

        public void Finish()
        {
            MessageBuffer<InteractionEndMessage>.Dispatch();
            OnFinishInteract?.Invoke();
        }


        // --------------------------------------------------------------------

        public void SetInteractionData(InteractionData data)
        {
            Data = data;
            OnDataChanged?.Invoke();
        }

        // --------------------------------------------------------------------

        private void OnDrawGizmos()
        {
            if (TryGetComponent(out Collider collider))
            {
                DebugUtils.DrawCollider(collider, new Color(0f, 1f, 0f, 0.5f), 0);
            }
            
        }
    }
}