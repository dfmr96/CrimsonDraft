using System.Collections.Generic;

namespace HorrorEngine
{

    public class MessageBuffer<T> where T : BaseMessage, new()
    {
        public delegate void MessageCallback(T ev);

        public static T DefaultEventParams = new T();

        private static HashSet<MessageCallback> mCallbacks = new HashSet<MessageCallback>();
        private static HashSet<MessageCallback> mCallbacksToAdd = new HashSet<MessageCallback>();
        private static HashSet<MessageCallback> mCallbacksToRemove = new HashSet<MessageCallback>();
        private static bool mDispatchInProgress = false;

        // --------------------------------------------------------------------

        public static void Subscribe(MessageCallback ev)
        {
            if (mDispatchInProgress)
            {
                mCallbacksToAdd.Add(ev);
            }
            else
            {
                mCallbacks.Add(ev);
            }
        }

        // --------------------------------------------------------------------

        public static void Unsubscribe(MessageCallback ev)
        {
            if (mDispatchInProgress)
            {
                mCallbacksToRemove.Add(ev);
            }
            else
            {
                mCallbacks.Remove(ev);
            }
        }

        // --------------------------------------------------------------------

        public static void Dispatch(T ev)
        {
            mDispatchInProgress = true;


            foreach (MessageCallback callback in mCallbacks)
            {
                callback(ev);
            }

            foreach (MessageCallback callbackToRemove in mCallbacksToRemove)
            {
                mCallbacks.Remove(callbackToRemove);
            }

            foreach (MessageCallback callbackToAdd in mCallbacksToAdd)
            {
                mCallbacks.Add(callbackToAdd);
            }

            mCallbacksToRemove.Clear();
            mCallbacksToAdd.Clear();

            mDispatchInProgress = false;
        }

        // --------------------------------------------------------------------

        public static void Dispatch()
        {
            Dispatch(DefaultEventParams);
        }
    }
}