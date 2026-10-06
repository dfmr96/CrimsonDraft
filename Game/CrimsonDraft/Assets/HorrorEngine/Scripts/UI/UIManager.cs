using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    public struct UIStackedAction
    {
        public string Name;
        public UnityAction Action;
        public bool StopProcessingActions;
    }

    public class UIManager : SingletonBehaviour<UIManager>
    {
        private Dictionary<Type, Component> m_CachedUI = new Dictionary<Type, Component>();
        private List<UIStackedAction> m_ActionStack = new List<UIStackedAction>();
        private Queue<UIStackedAction> m_PendingQueue = new Queue<UIStackedAction>();
        private bool m_ProcessingEnabled = true;
        private bool m_ProcessingStack = false;

        public static T Get<T>(bool optional = false) where T:Component
        {
            UIManager ui = Instance;
            Type type = typeof(T);
            if (!ui.m_CachedUI.ContainsKey(type))
            {
                var component = ui.GetComponentInChildren<T>(true);
                if (component)
                {
                    ui.m_CachedUI.Add(type, component);
                }
            }

            ui.m_CachedUI.TryGetValue(type, out Component value);
            if (!optional)
                Debug.Assert(value, $"UIManager couldn't find a component of type {type.Name}");

            return value as T;
        } 

        public static void PushAction(UIStackedAction item)
        {
            UIManager ui = Instance;
            if (!ui.m_ProcessingStack)
                ui.m_ActionStack.Add(item);
            else
                ui.m_PendingQueue.Enqueue(item);
        }

        public static void RemoveAction(UIStackedAction item)
        {
            Instance.m_ActionStack.Remove(item);   
        }

        public static bool PopAction()
        {
            UIManager ui = Instance;
            if (!ui.m_ProcessingEnabled)
                return false;

            int processed = 0;
            UIStackedAction item;

            ui.m_ProcessingStack = true;
            do
            {
                if (ui.m_ActionStack.Count == 0)
                {
                    break;
                }
               
                item = ui.m_ActionStack[ui.m_ActionStack.Count - 1];
                ui.m_ActionStack.RemoveAt(ui.m_ActionStack.Count - 1);

                item.Action?.Invoke();
                ++processed;
                
            } while (!item.StopProcessingActions);

            ui.m_ProcessingStack = false;

            if (ui.m_PendingQueue.Count > 0)
            {
                int pending = ui.m_PendingQueue.Count;
                for (int i = 0; i < pending; ++i)
                {
                    ui.m_ActionStack.Add(ui.m_PendingQueue.Dequeue());
                }
            }

            return processed > 0;
        }

        public static void SetActionProcessingEnabled(bool enabled)
        {
            Instance.m_ProcessingEnabled = enabled;
        }
    }
}