using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HorrorEngine
{
    public class EventSystemUtils : MonoBehaviour
    {
        public static void SelectDefaultOnLostFocus(GameObject defaultObj)
        {
            if (!EventSystem.current)
                return;

            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null || !selected.activeInHierarchy)
            {
                ForceSelection(defaultObj);
            }
        }

        public static void ForceSelection(GameObject selection)
        {
            if (!EventSystem.current)
                return;

            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(selection);
        }
    }
}