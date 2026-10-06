using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    public class UIContextAddedMessage : BaseMessage
    {
        public UIContext Context;
    }
    public class UIContextRemovedMessage : BaseMessage
    {
        public UIContext Context;
    }

    public class UIContext : MonoBehaviour
    {
        private UIContextAddedMessage m_AddedMsg = new UIContextAddedMessage();
        private UIContextRemovedMessage m_RemovedMsg = new UIContextRemovedMessage();

        public UIContextHandle ContextHandle;
        public List<UIContextHandle> BlockingContexts;
        
        private static Dictionary<UIContextHandle, UIContext> m_ContextList = new Dictionary<UIContextHandle, UIContext>();

        public UnityEvent OnBlocked;
        public UnityEvent OnUnblocked;

        private bool m_IsBlocked;

        private MessageBuffer<UIContextAddedMessage>.MessageCallback OnContextAddedCallback;
        private MessageBuffer<UIContextRemovedMessage>.MessageCallback OnContextRemovedCallback;

        private void Awake()
        {
            OnContextAddedCallback = OnContextAdded;
            OnContextRemovedCallback = OnContextRemoved;
        }

        public void Activate()
        {
            if (m_ContextList.ContainsKey(ContextHandle))
            {
                Debug.LogError($"UIContext already contained handle {ContextHandle.name}");
                return;
            }

            m_ContextList.Add(ContextHandle, this);

            MessageBuffer<UIContextAddedMessage>.Subscribe(OnContextAddedCallback);
            MessageBuffer<UIContextRemovedMessage>.Subscribe(OnContextRemovedCallback);

            m_AddedMsg.Context = this;
            MessageBuffer<UIContextAddedMessage>.Dispatch(m_AddedMsg);

            UpdateBlockedState();
        }

        private void UpdateBlockedState()
        {
            if (IsBlocked())
            {
                SetBlocked();
            }
            else
            {
                SetUnblocked();
            }
        }

        public void Deactivate()
        {
            MessageBuffer<UIContextAddedMessage>.Unsubscribe(OnContextAddedCallback);
            MessageBuffer<UIContextRemovedMessage>.Unsubscribe(OnContextRemovedCallback);

            m_ContextList.Remove(ContextHandle);
            
            m_RemovedMsg.Context = this;
            MessageBuffer<UIContextRemovedMessage>.Dispatch(m_RemovedMsg);
        }

        private void SetBlocked()
        {
            if (!m_IsBlocked)
            {
                m_IsBlocked = true;
                OnBlocked?.Invoke();
            }
        }


        private void SetUnblocked()
        {
            if (m_IsBlocked)
            {
                m_IsBlocked = false;
                OnUnblocked?.Invoke();
            }
        }

        private void OnContextRemoved(UIContextRemovedMessage ev)
        {
            if (ev.Context != this)
            {
                UpdateBlockedState();
            }
        }

        private void OnContextAdded(UIContextAddedMessage ev)
        {
            if (ev.Context != this)
            {
                UpdateBlockedState();
            }
        }

        public bool IsBlocked()
        {
            foreach (var handle in BlockingContexts)
            {
                if (m_ContextList.ContainsKey(handle))
                {
                    return true;
                }
            }

            return false;
        }
    }
}