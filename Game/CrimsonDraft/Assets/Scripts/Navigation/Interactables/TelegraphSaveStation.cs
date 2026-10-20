#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Cinemachine;
using UnityEngine;
using VContainer;
using Yarn.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Save;
using CrimsonDraft.Infrastructure.Save.UI;
using CrimsonDraft.UI;

namespace CrimsonDraft.Navigation.Interactables
{
    /// <summary>
    /// Save-room wiring that sits next to a <see cref="SavePointInteractable"/>: the camera it
    /// cuts to, the world-space SaveGame_canva panel, and the one-time telegraph-room intro.
    /// Without this component the save point falls back to the screen-overlay save menu.
    /// </summary>
    public sealed class TelegraphSaveStation : MonoBehaviour, ITelegraphMorse
    {
        // The intro has no world object of its own to track, so its "seen" flag borrows the
        // pickup registry -- already saved, loaded and reset together with a save file.
        private const string IntroSeenId = "intro/telegraph_room";

        [SerializeField] private CinemachineCamera     telegraphCamera = null!;
        [SerializeField] private LoadGameSaveListView  saveView        = null!;
        [SerializeField] private DialogueReference     introDialogue   = new();

        // The telegraph key moves and beeps in Morse while a save is written. Optional.
        [Header("Morse key")]
        [SerializeField] private MorseKeyAnimator? morseKey;
        [Tooltip("Duracion de una unidad de Morse (un punto). Marca el ritmo: el texto del save se escribe en el mismo tiempo que dura el mensaje.")]
        [SerializeField] private float unitSeconds = 0.1f;

        private WorldStateRegistries world = null!;
        private InventorySfxData?    sfx;
        private bool                 keyDown;

        public CinemachineCamera    Camera        => this.telegraphCamera;
        public LoadGameSaveListView View          => this.saveView;
        public string?              IntroNodeName => this.introDialogue.nodeName;

        public bool IntroSeen => this.world.Pickups.IsCollected(IntroSeenId);

        [Inject]
        public void Construct(WorldStateRegistries world, InventorySfxData? sfx = null)
        {
            this.world = world;
            this.sfx   = sfx;
        }

        public float MeasureSeconds(string text) =>
            this.morseKey == null ? 0f : MorseEncoder.TotalUnits(MorseEncoder.Encode(text)) * this.unitSeconds;

        public async UniTask TransmitAsync(string text, CancellationToken cancellationToken)
        {
            if (this.morseKey == null) return;

            var steps = MorseEncoder.Encode(text);
            if (steps.Count == 0) return;

            float unit = this.unitSeconds;

            try
            {
                foreach (var step in steps)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    float seconds = step.Units * unit;

                    if (step.Tone) PressKey(true, seconds);
                    await UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.UnscaledDeltaTime, cancellationToken: cancellationToken);
                    if (step.Tone) PressKey(false, unit);
                }
            }
            finally
            {
                StopTransmission();
            }
        }

        public void StopTransmission()
        {
            if (this.keyDown) PressKey(false, 0.04f);
        }

        private void PressKey(bool down, float seconds)
        {
            this.keyDown = down;
            this.morseKey!.SetPressed(down, seconds * 0.5f);

            if (down) this.sfx?.PlayBeeperMorseStart(this.morseKey.gameObject);
            else      this.sfx?.StopBeeperMorse(this.morseKey.gameObject);
        }

        private void Awake()
        {
            // Kept off until InspectionController cuts to it (see MusicBoxInteractable).
            this.telegraphCamera.enabled = false;
        }

        public void MarkIntroSeen() => this.world.Pickups.SetCollected(IntroSeenId);
    }
}
