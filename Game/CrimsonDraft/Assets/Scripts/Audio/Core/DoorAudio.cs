#nullable enable

using UnityEngine;

namespace CrimsonDraft.Audio
{
    // Posts a single event per interaction. The Wwise-side container (Door > OpenCloseDoor)
    // is a Step+Sequence Random Sequence Container, so each Post() alternates between the
    // Open and Close sounds automatically — this only works if Play() is called exactly once
    // per open and once per close, in that order.
    public sealed class DoorAudio : MonoBehaviour
    {
        [Header("Wwise")]
        [SerializeField] private WwiseTrigger openCloseTrigger = new();
        [SerializeField] private WwiseTrigger resumeAllTrigger = new();

        public void Play() => openCloseTrigger.FireWithEndCallback(gameObject, OnOpenCloseEnd);

        private void OnOpenCloseEnd(object cookie, AkCallbackType type, AkCallbackInfo info)
            => resumeAllTrigger.Fire(gameObject);
    }
}
