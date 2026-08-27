#nullable enable

using CrimsonDraft.Infrastructure.Input;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;

namespace CrimsonDraft.UI.MainMenu
{
    public sealed class MainMenuCameraTravel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform  cameraTransform = null!; // Main_Menu-Camera -- the camera that actually moves.
        [SerializeField] private Transform  newGamePose     = null!; // New_Game-Camera -- position/rotation marker only, not a live camera.
        [SerializeField] private GameObject titleCanvas     = null!;
        [SerializeField] private GameObject newGameCanvas   = null!;
        [Tooltip("Selectable enfocado al llegar al canvas de New Game -- sin esto el EventSystem se queda sin selección y Navigate/Submit/Cancel no tienen nada sobre lo que actuar.")]
        [SerializeField] private Selectable newGameFirstSelected = null!;

        [Header("Travel")]
        [SerializeField] private float travelDuration = 1.5f;
        [SerializeField] private Ease  travelEase      = Ease.InOutSine;

        private IInputService inputService = null!;
        private Vector3       homePosition;
        private Quaternion    homeRotation;
        private bool          isAtNewGame;
        private bool          isTravelling;

        [Inject]
        public void Construct(IInputService inputService)
        {
            this.inputService = inputService;
            this.inputService.UICancel.performed += OnCancel;
        }

        private void Awake()
        {
            this.homePosition = this.cameraTransform.position;
            this.homeRotation = this.cameraTransform.rotation;
        }

        private void OnDestroy()
        {
            if (this.inputService == null) return;
            this.inputService.UICancel.performed -= OnCancel;
        }

        private void OnDisable() => DOTween.Kill(this);

        public void TravelToNewGame()
        {
            if (this.isTravelling || this.isAtNewGame) return;
            this.isTravelling = true;
            this.isAtNewGame  = true;

            this.titleCanvas.SetActive(false);
            EventSystem.current?.SetSelectedGameObject(null);

            DOTween.Sequence()
                .SetTarget(this)
                .Append(this.cameraTransform.DOMove(this.newGamePose.position, this.travelDuration).SetEase(this.travelEase))
                .Join(this.cameraTransform.DORotateQuaternion(this.newGamePose.rotation, this.travelDuration).SetEase(this.travelEase))
                .OnComplete(() =>
                {
                    this.isTravelling = false;
                    this.newGameCanvas.SetActive(true);
                    EventSystem.current?.SetSelectedGameObject(this.newGameFirstSelected.gameObject);
                });
        }

        private void OnCancel(InputAction.CallbackContext _)
        {
            if (!this.isAtNewGame || this.isTravelling) return;
            TravelBack();
        }

        private void TravelBack()
        {
            this.isTravelling = true;
            this.isAtNewGame  = false;

            this.newGameCanvas.SetActive(false);
            EventSystem.current?.SetSelectedGameObject(null);

            DOTween.Sequence()
                .SetTarget(this)
                .Append(this.cameraTransform.DOMove(this.homePosition, this.travelDuration).SetEase(this.travelEase))
                .Join(this.cameraTransform.DORotateQuaternion(this.homeRotation, this.travelDuration).SetEase(this.travelEase))
                .OnComplete(() =>
                {
                    this.isTravelling = false;
                    this.titleCanvas.SetActive(true);
                });
        }
    }
}
