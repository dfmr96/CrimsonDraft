using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    public class ObjectMessageBuffer : MonoBehaviour
    {
        private Dictionary<Type, List<Delegate>> mListeners = new Dictionary<Type, List<Delegate>>();
        //private Dictionary<Type, List<Delegate>> mListenersToRemove = new Dictionary<Type, List<Delegate>>();
        //private Dictionary<Type, List<Delegate>> mListenersToAdd = new Dictionary<Type, List<Delegate>>();
        //private bool m_DispatchInProgress;

        // --------------------------------------------------------------------

        public void Dispatch<T>(T message) where T : BaseMessage, new()
        {
            //m_DispatchInProgress = true;

            List<Delegate> callbacks = null;
            mListeners.TryGetValue(typeof(T), out callbacks);
            if (callbacks == null)
                return;

            for (int i = 0; i < callbacks.Count; ++i)
            {
                MessageBuffer<T>.MessageCallback callback = (MessageBuffer<T>.MessageCallback)callbacks[i];
                callback(message);
            }

            /*
            foreach (var listenerToRemoveType in mListenersToRemove.Keys)
            {
                mListeners.Remove(callbackToRemove);
            }

            foreach (MessageCallback callbackToAdd in mCallbacksToAdd)
            {
                mCallbacks.Add(callbackToAdd);
            }

            mListenersToRemove.Clear();
            mListenersToAdd.Clear();
            */

            //m_DispatchInProgress = false;
        }

        // --------------------------------------------------------------------

        public void Subscribe<T>(MessageBuffer<T>.MessageCallback callback) where T : BaseMessage, new()
        {
            if (!mListeners.ContainsKey(typeof(T)))
            {
                mListeners.Add(typeof(T), new List<Delegate>());
            }

            mListeners[typeof(T)].Add(callback);
        }

        // --------------------------------------------------------------------

        public void Unsubscribe<T>(MessageBuffer<T>.MessageCallback callback) where T : BaseMessage, new()
        {
            if (mListeners.ContainsKey(typeof(T)))
                mListeners[typeof(T)].Remove(callback);
        }
    }
}