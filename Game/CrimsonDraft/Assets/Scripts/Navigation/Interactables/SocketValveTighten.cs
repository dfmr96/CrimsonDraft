#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using CrimsonDraft.Navigation.Dialogue;

namespace CrimsonDraft.Navigation.Interactables
{
    /// <summary>Second step of the pipe-valve socket (KI_11). Filling the socket shows the
    /// "You placed X" line; once it closes the player is asked whether to turn the handle
    /// (and again on any later interaction with the socket if they said "No"). On "Yes" the
    /// dialogue closes, the handle turns, the steam particle system stops emitting and a
    /// follow-up line plays; the socket then records the turn (ItemSocketInteractable.MarkTurned)
    /// so it stays done across scene changes and saves. If it was already turned when this loads,
    /// the end state is applied instantly instead -- the player already saw the animation and
    /// line. Kept separate from ItemSocketInteractable itself -- other sockets (e.g. the
    /// Generator's) don't have this extra step.</summary>
    public sealed class SocketValveTighten : MonoBehaviour
    {
        [SerializeField] private ItemSocketInteractable socket   = null!;
        [SerializeField] private Transform               handle  = null!;
        [SerializeField] private ParticleSystem          steam   = null!;
        [SerializeField] private Vector3 tightenEulerDelta = new(0f, 45f, 0f);
        [SerializeField] private float   tightenDuration   = 0.6f;
        [SerializeField] private string  turnPromptNodeName = "socket_pipe_valve_turn";
        [SerializeField] private string  adjustedNodeName  = "socket_pipe_valve_adjusted";

        private IDialogueService       dialogueService       = null!;
        private IPickupDialogueService pickupDialogueService = null!;

        private bool         started;
        private bool         tightened; // handle already at its final rotation

        [Inject]
        public void Construct(IDialogueService dialogueService, IPickupDialogueService pickupDialogueService)
        {
            this.dialogueService       = dialogueService;
            this.pickupDialogueService = pickupDialogueService;
        }

        void Start()
        {
            this.started = true;
            ApplyFinalState();
        }

        // The room may be switched off and on again: the steam's play-on-awake would restart it.
        void OnEnable()
        {
            if (this.started) ApplyFinalState();
        }

        // Restored-turned (or already turned this session) -- snap straight to the end state,
        // no replay of the turn/stop/line beat.
        private void ApplyFinalState()
        {
            if (!this.socket.IsTurned) return;

            if (!this.tightened)
            {
                this.handle.localRotation *= Quaternion.Euler(this.tightenEulerDelta);
                this.tightened = true;
            }

            var main = this.steam.main;
            main.playOnAwake = false;
            this.steam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // Called by the socket right after its insert line closes, and again on any later
        // interaction while the handle is still unturned (the player answered "No").
        // Uses the Yes/No panel (PickupDialogueService), same as the liquid dispensers. The turn
        // starts from onComplete, once the panel has closed and input is back in Gameplay.
        public void PromptTurn()
        {
            bool turn = false;

            this.pickupDialogueService.StartDialogue(
                this.turnPromptNodeName,
                onComplete: () =>
                {
                    if (turn) StartCoroutine(TightenRoutine());
                },
                commands: new Dictionary<string, Action>
                {
                    ["turn_valve"] = () => turn = true
                });
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
            this.tightened = true;

            this.steam.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            this.socket.MarkTurned();

            this.dialogueService.StartDialogue(this.adjustedNodeName);
        }
    }
}
