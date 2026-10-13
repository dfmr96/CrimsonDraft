#nullable enable

using System;
using System.Collections;
using MessagePipe;
using UnityEngine;
using VContainer;
using CrimsonDraft.Navigation.Dialogue;

namespace CrimsonDraft.Navigation.Interactables
{
    /// <summary>Plays the "tightening the valve" beat after this pipe-valve socket (KI_11) is
    /// filled: once the socket's own "You inserted X" dialogue closes, the handle gives a small
    /// extra turn; once that finishes, the steam particle system stops emitting and a follow-up
    /// line plays. Kept separate from ItemSocketInteractable itself -- other sockets (e.g. the
    /// Generator's) fire onActivated immediately alongside their own dialogue, and this timing
    /// shouldn't change for those. If the socket was already filled when this loads (restored
    /// from a save), the end state is applied instantly instead -- the player already saw the
    /// animation and line in the session that filled it.</summary>
    public sealed class SocketValveTighten : MonoBehaviour
    {
        [SerializeField] private ItemSocketInteractable socket   = null!;
        [SerializeField] private Transform               handle  = null!;
        [SerializeField] private ParticleSystem          steam   = null!;
        [SerializeField] private Vector3 tightenEulerDelta = new(0f, 45f, 0f);
        [SerializeField] private float   tightenDuration   = 0.6f;
        [SerializeField] private string  adjustedNodeName  = "socket_pipe_valve_adjusted";

        [Inject] private IDialogueService                        dialogueService = null!;
        [Inject] private ISubscriber<DialogueActiveChangedEvent> dialogueActiveSub = null!;

        private IDisposable? subscription;
        private bool         waitingForInsertDialogueClose;

        void Awake() => this.socket.OnActivated.AddListener(OnSocketActivated);

        void Start()
        {
            this.subscription = this.dialogueActiveSub.Subscribe(OnDialogueActiveChanged);

            // Restored-complete from a previous session -- snap straight to the end state,
            // no replay of the turn/stop/line beat.
            if (this.socket.IsActivated)
            {
                this.handle.localRotation *= Quaternion.Euler(this.tightenEulerDelta);
                this.steam.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        void OnDestroy() => this.subscription?.Dispose();

        private void OnSocketActivated() => this.waitingForInsertDialogueClose = true;

        private void OnDialogueActiveChanged(DialogueActiveChangedEvent e)
        {
            if (!this.waitingForInsertDialogueClose || e.IsActive) return;

            this.waitingForInsertDialogueClose = false;
            StartCoroutine(TightenRoutine());
        }

        private IEnumerator TightenRoutine()
        {
            Quaternion start = this.handle.localRotation;
            Quaternion end   = start * Quaternion.Euler(this.tightenEulerDelta);

            float t = 0f;
            while (t < this.tightenDuration)
            {
                t += Time.deltaTime;
                this.handle.localRotation = Quaternion.Slerp(start, end, t / this.tightenDuration);
                yield return null;
            }
            this.handle.localRotation = end;

            this.steam.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            this.dialogueService.StartDialogue(this.adjustedNodeName);
        }
    }
}
