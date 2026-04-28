using UnityEngine;

namespace HorrorEngine
{
    public class OnAnimatorLayerBlendStartedMessage : BaseMessage
    {
        public int LayerIndex;
        public float Duration;
        public float FromWeight = -1;
        public float ToWeight;
        public AnimationCurve BlendCurve;
        public bool StartFromCurrentWeight;
    }

    public class AnimatorLayerBlend : MonoBehaviour
    {
        [SerializeField] protected AnimatorLayerHandle m_Layer;
        [SerializeField] protected float m_FromWeight = -1;
        [SerializeField] protected float m_ToWeight;
        [SerializeField] protected AnimationCurve m_BlendCurve = AnimationCurve.Linear(0, 0, 0.25f, 1);
        [SerializeField] protected bool m_StartFromCurrentWeight = true;

        private ObjectMessageBuffer m_ObjMsgBuffer;
        private OnAnimatorLayerBlendStartedMessage m_BlendMsg = new OnAnimatorLayerBlendStartedMessage();

        // --------------------------------------------------------------------

        protected virtual void Awake()
        {
            m_ObjMsgBuffer = GetComponentInParent<ObjectMessageBuffer>();
            Debug.Assert(m_ObjMsgBuffer, "ObjectMessageBuffer component doesn't exist in the object", gameObject);
        }

        // --------------------------------------------------------------------

        public virtual void Trigger()
        {
            m_BlendMsg.LayerIndex = m_Layer.Index;
            m_BlendMsg.Duration = m_BlendCurve.GetDuration();
            m_BlendMsg.ToWeight = m_ToWeight;
            m_BlendMsg.FromWeight = m_FromWeight;

            m_BlendMsg.BlendCurve = m_BlendCurve;
            m_BlendMsg.StartFromCurrentWeight = m_StartFromCurrentWeight;

            m_ObjMsgBuffer.Dispatch(m_BlendMsg);
        }
    }
}