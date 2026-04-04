using System;
using UnityEngine;
using System.Collections;

namespace HorrorEngine
{
    public static class MonoBehaviourExtensions
    {
        public static Coroutine InvokeActionNextFrame(this MonoBehaviour me, Action theDelegate)
        {
            return me.StartCoroutine(ExecuteAfterFrame(theDelegate));
        }

        // --------------------------------------------------------------------

        public static Coroutine InvokeAction(this MonoBehaviour me, Action theDelegate, float time)
        {
            return me.StartCoroutine(ExecuteAfterTime(theDelegate, time));
        }

        // --------------------------------------------------------------------

        public static Coroutine InvokeActionUnscaled(this MonoBehaviour me, Action theDelegate, float time)
        {
            return me.StartCoroutine(ExecuteAfterUnscaledTime(theDelegate, time));
        }

        // --------------------------------------------------------------------

        public static Coroutine InvokeActionRepeating(this MonoBehaviour me, Action theDelegate, float interval, float initDelay = 0)
        {
            return me.StartCoroutine(ExecuteInIntervals(theDelegate, interval, initDelay));
        }

        // --------------------------------------------------------------------

        private static IEnumerator ExecuteInIntervals(Action theDelegate, float interval, float initDelay)
        {
            yield return Yielders.Time(initDelay);

            while (true)
            {
                theDelegate();
                yield return Yielders.Time(interval);
            }
        }

        // --------------------------------------------------------------------

        private static IEnumerator ExecuteAfterTime(Action theDelegate, float delay)
        {
            yield return Yielders.Time(delay);
            theDelegate();
        }

        // --------------------------------------------------------------------

        private static IEnumerator ExecuteAfterUnscaledTime(Action theDelegate, float delay)
        {
            yield return Yielders.UnscaledTime(delay);
            theDelegate();
        }


        // --------------------------------------------------------------------

        private static IEnumerator ExecuteAfterFrame(Action theDelegate)
        {
            yield return Yielders.EndOfFrame;
            theDelegate();
        }
    }
}