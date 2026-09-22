#nullable enable

using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Navigation.CamaraSystem;
using CrimsonDraft.Navigation.Enemy;
using CrimsonDraft.Navigation.Player;

namespace CrimsonDraft.Navigation.Interactables
{
    // Drives the "examine → confirm → hard-cut to the object's own camera" flow shared by
    // inspectable puzzle props (e.g. GeneratorInteractable). Mirrors PuzzleViewController:
    // a scoped service that owns an input map switch and a UICancel subscription for the
    // whole time the inspection view is open.
    //
    // Routes the cut through IFixedCameraZoneService -- the same mechanism the level's room
    // shots use -- instead of swapping the physical Camera component (an earlier version did
    // that via ICameraService, but it disabled the "Camera" GameObject entirely, which is the
    // URP camera-stack Base that "UI CRT Camera" (an Overlay) is stacked onto; with Base gone
    // the whole dialogue/UI canvas stack silently stopped rendering, even though the input map
    // switch still worked -- the game looked "frozen" with no visible prompt). FixedCameraZone
    // only ever toggles CinemachineCamera.enabled on vcams that all still feed the same
    // physical Base camera via CinemachineBrain, so the camera stack is never touched.
    public sealed class InspectionController : IInitializable, IDisposable
    {
        private readonly IInputService          inputService;
        private readonly IFixedCameraZoneService zoneService;
        private readonly PlayerController        player;
        private readonly EnemyNavAgent[]         enemies;

        private bool               isInspecting;
        private Action?            onExit;
        private CinemachineCamera? previousZoneCamera;

        [Preserve]
        public InspectionController(
            IInputService     inputService,
            IFixedCameraZoneService zoneService,
            PlayerController  player,
            EnemyNavAgent[]   enemies)
        {
            this.inputService = inputService;
            this.zoneService  = zoneService;
            this.player       = player;
            this.enemies      = enemies;
        }

        public bool IsInspecting => this.isInspecting;

        void IInitializable.Initialize()
        {
            this.inputService.UICancel.performed += OnCancel;
        }

        // onExit lets a caller-specific object (e.g. GeneratorInteractable's switch-panel
        // navigator) clean itself up whenever inspection ends, however it ends -- Cancel (B)
        // here, or a future programmatic Exit() -- without InspectionController itself needing
        // to know about anything puzzle-specific.
        public void Enter(CinemachineCamera inspectCamera, Action? onExit = null)
        {
            if (this.isInspecting) return;

            this.isInspecting       = true;
            this.onExit             = onExit;
            this.previousZoneCamera = this.zoneService.CurrentZoneCamera;
            this.zoneService.ActivateZone(inspectCamera);
            this.inputService.SwitchToUI();
            SetActorRenderersEnabled(false);
        }

        private void OnCancel(InputAction.CallbackContext _)
        {
            if (!this.isInspecting) return;
            Exit();
        }

        // Programmatic equivalent of pressing Cancel -- e.g. GeneratorSwitchPanel force-closing
        // the view once the puzzle is solved.
        public void ExitNow()
        {
            if (!this.isInspecting) return;
            Exit();
        }

        private void Exit()
        {
            this.isInspecting = false;
            if (this.previousZoneCamera != null)
                this.zoneService.ActivateZone(this.previousZoneCamera);
            this.previousZoneCamera = null;
            this.inputService.SwitchToGameplay();
            SetActorRenderersEnabled(true);

            var callback = this.onExit;
            this.onExit  = null;
            callback?.Invoke();
        }

        // The inspect camera hard-cuts to a shot the designer framed around the puzzle prop
        // alone -- the player rig (and any enemy nearby) would otherwise stand in the middle
        // of it. Queried live rather than cached at construction time since enemies can be
        // defeated (destroyed) between inspections.
        private void SetActorRenderersEnabled(bool enabled)
        {
            foreach (var renderer in this.player.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = enabled;

            foreach (var enemy in this.enemies)
            {
                if (enemy == null) continue;
                foreach (var renderer in enemy.GetComponentsInChildren<Renderer>(true))
                    renderer.enabled = enabled;
            }
        }

        void IDisposable.Dispose()
        {
            this.inputService.UICancel.performed -= OnCancel;
        }
    }
}
