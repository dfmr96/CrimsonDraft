using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace HorrorEngine
{
   
    [System.Serializable]
    public class ActorTeleportClip : PlayableAsset, ITimelineClipAsset
    {
        [SerializeField] ExposedReference<Transform> m_TeleportPoint;
        

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<ActorTeleportClipBehaviour>.Create(graph);

            ActorTeleportClipBehaviour playableBehaviour = playable.GetBehaviour();
            playableBehaviour.TeleportPoint = m_TeleportPoint.Resolve(graph.GetResolver());
            
            return playable;
        }
    }

    [System.Serializable]
    public class ActorTeleportClipBehaviour : ActorClipBehaviour
    {
        public Transform TeleportPoint;

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            base.OnBehaviourPlay(playable, info);

            if (!Application.isPlaying)
            {
                return;
            }

            var CharCtrl = m_Actor.GetComponent<CharacterController>();
            if (CharCtrl)
                CharCtrl.enabled = false;

            m_Actor.PlaceAt(TeleportPoint);

            if (CharCtrl)
                CharCtrl.enabled = true;
        }
    }
}