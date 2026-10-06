using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    public class WaitForMessageBufferEvent<T> : CustomYieldInstruction where T : BaseMessage, new()
    {
        private int dismissedFramecount = 0;

        public override bool keepWaiting
        {
            get
            {
                return !(dismissedFramecount == Time.frameCount);
            }
        }

        public WaitForMessageBufferEvent()
        {
            MessageBuffer<T>.Subscribe(OnDialogDismiss);
        }

        private void OnDialogDismiss(T msg)
        {
            dismissedFramecount = Time.frameCount;
        }
    }

    public static class Yielders
    {
        private class FloatComparer : IEqualityComparer<float>
        {
            bool IEqualityComparer<float>.Equals(float x, float y)
            {
                return x == y;
            }

            int IEqualityComparer<float>.GetHashCode(float obj)
            {
                return obj.GetHashCode();
            }
        }

        private static Dictionary<float, WaitForSeconds> _timeInterval = new Dictionary<float, WaitForSeconds>(25, new FloatComparer());
        private static Dictionary<float, WaitForSecondsRealtime> _timeIntervalUnscaled = new Dictionary<float, WaitForSecondsRealtime>(25, new FloatComparer());

        private static WaitForEndOfFrame _endOfFrame = new WaitForEndOfFrame();

        public static WaitForEndOfFrame EndOfFrame
        {
            get { return _endOfFrame; }
        }

        private static WaitForFixedUpdate _fixedUpdate = new WaitForFixedUpdate();

        public static WaitForFixedUpdate FixedUpdate
        {
            get { return _fixedUpdate; }
        }

        public static WaitForSeconds Time(float seconds)
        {
            WaitForSeconds wfs;
            if (!_timeInterval.TryGetValue(seconds, out wfs))
                _timeInterval.Add(seconds, wfs = new WaitForSeconds(seconds));
            return wfs;
        }
        public static WaitForSecondsRealtime UnscaledTime(float seconds)
        {
            WaitForSecondsRealtime wfsr;
            if (!_timeIntervalUnscaled.TryGetValue(seconds, out wfsr))
                _timeIntervalUnscaled.Add(seconds, wfsr = new WaitForSecondsRealtime(seconds));
            return wfsr;
        }
    }
}