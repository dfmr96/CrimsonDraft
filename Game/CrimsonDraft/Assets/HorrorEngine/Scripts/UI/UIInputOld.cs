using UnityEngine;

namespace HorrorEngine
{
    public class UIInputOld : MonoBehaviour, IUIInput
    {
        [SerializeField] string m_XPrimaryAxis;
        [SerializeField] string m_YPrimaryAxis;
        [SerializeField] string m_Confirm;
        [SerializeField] string m_Cancel;
        [SerializeField] string m_TogglePause;
        [SerializeField] string m_ToggleInventory;
        [SerializeField] string m_ToggleMap;
        [SerializeField] string m_ToggleMapList;
        [SerializeField] string m_NextSubmap;
        [SerializeField] string m_PrevSubmap;

        public Vector2 GetPrimaryAxis()
        {
            return new Vector2(Input.GetAxis(m_XPrimaryAxis), Input.GetAxis(m_YPrimaryAxis));
        }

        public bool IsConfirmDown()
        {
            return Input.GetButtonDown(m_Confirm);
        }

        public bool IsCancelDown()
        {
            return Input.GetButtonDown(m_Cancel);
        }

        public bool IsTogglePauseDown()
        {
            return Input.GetButtonDown(m_TogglePause);
        }

        public bool IsToggleInventoryDown()
        {
            return Input.GetButtonDown(m_ToggleInventory);
        }

        public bool IsToggleMapDown()
        {
            return Input.GetButtonDown(m_ToggleMap);
        }

        public bool IsToggleMapListDown()
        {
            return Input.GetButtonDown(m_ToggleMapList);
        }

        public bool IsPrevSubmapDown()
        {
            return Input.GetButtonDown(m_PrevSubmap);
        }

        public bool IsNextSubmapDown()
        {
            return Input.GetButtonDown(m_NextSubmap);
        }

        public void Flush()
        {
            Input.ResetInputAxes();
        }

    }
}