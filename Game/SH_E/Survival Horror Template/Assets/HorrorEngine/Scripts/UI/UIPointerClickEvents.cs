using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

namespace HorrorEngine
{
    public class UIPointerClickEvents : MonoBehaviour, IPointerClickHandler
    {
        [FormerlySerializedAs("Button")]
        [SerializeField] private PointerEventData.InputButton m_Button;
        public UnityEvent OnClick;
        public UnityEvent OnDoubleClick;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == m_Button)
            {
                OnClick?.Invoke();
                if (eventData.clickCount == 2)
                    OnDoubleClick?.Invoke();
            }
        }
    }
}
