using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace HorrorEngine
{
    [System.Serializable]
    public class InstantiateClip : PlayableAsset, ITimelineClipAsset
    {
        public GameObject Prefab;
        public ExposedReference<ObjectInstantiator> Instantiator;

        public ClipCaps clipCaps => ClipCaps.None;
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<InstantiateBehaviour>.Create(graph);
            playable.GetBehaviour().Prefab = Prefab;
            playable.GetBehaviour().Instantiator = Instantiator.Resolve(graph.GetResolver());
            return playable;
        }
    }

    public class InstantiateBehaviour : PlayableBehaviour
    {
        public GameObject Prefab;
        public ObjectInstantiator Instantiator;

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            if (Application.isPlaying)
            {
                Instantiator.Instantiate(Prefab, out GameObject instance);
            }
        }

    }
}