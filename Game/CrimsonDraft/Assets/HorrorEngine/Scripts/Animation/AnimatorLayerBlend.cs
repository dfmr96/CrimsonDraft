using UnityEngine;

namespace HorrorEngine
{
    public class OnAnimatorLayerBlendStartedMessage : BaseMessage
    {
        public int LayerIndex;
        public float Duration;
        public float ToWeight;
    }

    public class AnimatorLayerBlend : MonoBehaviour
    {
        [SerializeField] protected AnimatorLayerHandle m_Layer;
        [SerializeField] private float m_Time;
        [SerializeField] protected float m_ToWeight;

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
            m_BlendMsg.Duration = m_Time;
            m_BlendMsg.ToWeight = m_ToWeight;

            m_ObjMsgBuffer.Dispatch(m_BlendMsg);
        }
    }
}