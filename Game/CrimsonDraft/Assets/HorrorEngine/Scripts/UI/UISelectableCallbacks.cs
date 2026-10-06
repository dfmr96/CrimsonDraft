using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace HorrorEngine
{
    [System.Serializable]
    public class OnSelectableEvent : UnityEvent<GameObject> { }

    public class UISelectableCallbacks : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        public OnSelectableEvent OnSelected = new OnSelectableEvent();
        public OnSelectableEvent OnDeselected = new OnSelectableEvent();

        private void OnEnable()
        {
            if (EventSystem.current && EventSystem.current.currentSelectedGameObject == gameObject)
                OnSelect(null);
        }

        private void OnDisable()
        {
            OnDeselect(null);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            OnDeselected?.Invoke(gameObject);
        }

        public void OnSelect(BaseEventData eventData)
        {
            OnSelected?.Invoke(gameObject);
        }
    }
}