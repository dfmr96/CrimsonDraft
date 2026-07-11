#nullable enable

using UnityEngine;

namespace CrimsonDraft.Audio
{
    [RequireComponent(typeof(TutorialUI))]
    public sealed class TutorialAudio : MonoBehaviour
    {
        [Header("Wwise")]
        [SerializeField] private AK.Wwise.Event openEvent  = new();
        [SerializeField] private AK.Wwise.Event closeEvent = new();

        public void PlayOpen()  => openEvent?.Post(gameObject);
        public void PlayClose() => closeEvent?.Post(gameObject);
    }
}
