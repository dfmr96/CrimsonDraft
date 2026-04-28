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
        
        private static List<UIContext> m_ContextList = new List<UIContext>();

        public UnityEvent OnBlocked;
        public UnityEvent OnUnblocked;

        private bool m_IsBlocked;

        private MessageBuffer<UIContextAddedMessage>.MessageCallback OnContextAddedCallback;
        private MessageBuffer<UIContextRemovedMessage>.MessageCallback OnContextRemovedCallback;

        public bool IsActive()
        {
            return m_ContextList.Contains(this);
        } 

        public void Activate()
        {
            if (OnContextAddedCallback == null)
                OnContextAddedCallback = OnContextAdded;
            if (OnContextRemovedCallback == null)
                OnContextRemovedCallback = OnContextRemoved;

            if (m_ContextList.Contains(this))
            {
                Debug.LogError($"UIContext already contained context {ContextHandle.name}");
                return;
            }

            m_ContextList.Add(this);

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

            m_ContextList.Remove(this);
            
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
                foreach (var context in m_ContextList)
                {
                    if (context.ContextHandle == handle)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}