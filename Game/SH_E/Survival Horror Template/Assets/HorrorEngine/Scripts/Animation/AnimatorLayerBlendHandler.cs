using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    public struct LayerBlend
    {
        public float StartTime;
        public float StartWeight;
        public float TargetWeight;
        public float Duration;
        public AnimationCurve BlendCurve;
    }

    [RequireComponent(typeof(Animator))]
    public class AnimatorLayerBlendHandler : MonoBehaviour, IResetable
    {
        private Animator m_Animator;
        private Dictionary<int, float> m_OriginalWeights = new Dictionary<int, float>();
        private Dictionary<int, LayerBlend> m_ActiveBlends = new Dictionary<int, LayerBlend>();
        private List<int> m_FinishedBlends = new List<int>();
        private ObjectMessageBuffer m_ObjMsgBuffer;
        private MessageBuffer<OnAnimatorLayerBlendStartedMessage>.MessageCallback m_OnAnimatorBlendStarted;

        // --------------------------------------------------------------------

        private void Awake()
        {
            m_OnAnimatorBlendStarted = OnAnimatorBlendStarted;

            m_ObjMsgBuffer = GetComponentInParent<ObjectMessageBuffer>();
            Debug.Assert(m_ObjMsgBuffer, "ObjectMessageBuffer component doesn't exist in the object", gameObject);
            if (m_ObjMsgBuffer)
                m_ObjMsgBuffer.Subscribe(m_OnAnimatorBlendStarted);

            m_Animator = GetComponentInChildren<Animator>();

            int layerCount = m_Animator.layerCount;
            for(int i = 0; i < layerCount; ++i)
            {
                float w = m_Animator.GetLayerWeight(i);
                m_OriginalWeights.Add(i, w);
            }
        }


        // --------------------------------------------------------------------

        private void OnDestroy()
        {
            if (m_ObjMsgBuffer)
                m_ObjMsgBuffer.Unsubscribe(m_OnAnimatorBlendStarted);
        }

        // --------------------------------------------------------------------

        private void OnAnimatorBlendStarted(OnAnimatorLayerBlendStartedMessage msg)
        {
            float startWeight = msg.StartFromCurrentWeight ? -1f : msg.FromWeight;
            StartBlend(msg.LayerIndex, startWeight, msg.ToWeight, msg.Duration, msg.BlendCurve);
        }

        // --------------------------------------------------------------------

        public void OnReset()
        {
            ClearAll();
        }

        // --------------------------------------------------------------------

        private void ClearAll()
        {
            m_ActiveBlends.Clear();

            foreach (var originalWeights in m_OriginalWeights)
            {
                m_Animator.SetLayerWeight(originalWeights.Key, originalWeights.Value);
            }

            m_FinishedBlends.Clear();
        }


        // --------------------------------------------------------------------

        public void RevertLayer(AnimatorLayerHandle layerHandle, float duration)
        {
            StartBlend(layerHandle.Index, m_Animator.GetLayerWeight(layerHandle.Index), m_OriginalWeights[layerHandle.Index], duration);
        }

        // --------------------------------------------------------------------

        public void StartBlend(int layerIndex, float fromWeight, float toWeight, float duration, AnimationCurve curve = null)
        {
            var newBlend = new LayerBlend()
            {
                StartTime = Time.time,
                StartWeight = fromWeight >= 0 ? fromWeight : m_Animator.GetLayerWeight(layerIndex),
                Duration = duration,
                TargetWeight = toWeight,
                BlendCurve = curve
            };

            if (m_ActiveBlends.ContainsKey(layerIndex))
            {
                m_ActiveBlends[layerIndex] = newBlend;
            }
            else
            {
                m_ActiveBlends.Add(layerIndex, newBlend);
            }

            enabled = true;
        }

        // --------------------------------------------------------------------

        public void StopBlend(AnimatorLayerHandle layerHandle)
        {
            StopBlend(layerHandle.Index);
        }

        // --------------------------------------------------------------------

        public void StopBlend(int layerIndex)
        {
            m_ActiveBlends.Remove(layerIndex);
        }

        // --------------------------------------------------------------------

        private void Update()
        {
            float currentTime = Time.time;
            
            foreach (var activeBlend in m_ActiveBlends)
            {
                int layerIndex = activeBlend.Key;
                var blend = activeBlend.Value;

                float timePassed = currentTime - blend.StartTime;
                float t = timePassed / blend.Duration;

                if (t < 1f)
                {
                    if (blend.BlendCurve != null)
                        t = blend.BlendCurve.Evaluate(t);

                    float weight = Mathf.Lerp(blend.StartWeight, blend.TargetWeight, t);
                    m_Animator.SetLayerWeight(layerIndex, weight);
                }
                else
                {
                    m_Animator.SetLayerWeight(layerIndex, blend.TargetWeight);
                    m_FinishedBlends.Add(layerIndex);
                }
            }

            foreach (var layerIndex in m_FinishedBlends)
                m_ActiveBlends.Remove(layerIndex);

            m_FinishedBlends.Clear();

            enabled = m_ActiveBlends.Count > 0;
        }

    }
}