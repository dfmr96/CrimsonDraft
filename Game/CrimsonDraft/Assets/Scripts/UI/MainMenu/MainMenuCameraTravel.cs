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
        private enum Destination { None, NewGame, Options, LoadGame }

        [Header("References")]
        [SerializeField] private Transform  cameraTransform = null!; // Main_Menu-Camera -- the camera that actually moves.
        [SerializeField] private Transform  newGamePose     = null!; // New_Game-Camera -- position/rotation marker only, not a live camera.
        [SerializeField] private Transform  optionsPose     = null!; // Options-Camera -- position/rotation marker only, not a live camera.
        [SerializeField] private Transform  loadGamePose    = null!; // LoadGame-Camera -- position/rotation marker only, not a live camera.
        [SerializeField] private GameObject titleCanvas     = null!;
        [SerializeField] private GameObject newGameCanvas   = null!;
        [SerializeField] private GameObject optionsCanvas   = null!;
        [SerializeField] private GameObject loadGameCanvas  = null!;
        [SerializeField] private OptionsTabController optionsController = null!;
        [Tooltip("Selectable enfocado al llegar al canvas de New Game -- sin esto el EventSystem se queda sin selección y Navigate/Submit/Cancel no tienen nada sobre lo que actuar.")]
        [SerializeField] private Selectable newGameFirstSelected = null!;
        [Tooltip("Con esto se abre la lista de saves al llegar a LoadGame_canva. El Load Game de LoadGame_canva maneja su propio Confirm/Cancel (navegación anidada lista/confirmar), así que OnCancel lo ignora por completo -- ver TravelBackFromLoadGame.")]
        [SerializeField] private MainMenuController mainMenuController = null!;

        [Header("Travel")]
        [SerializeField] private float travelDuration = 1.5f;
        [SerializeField] private Ease  travelEase      = Ease.InOutSine;

        private IInputService inputService = null!;
        private Vector3       homePosition;
        private Quaternion    homeRotation;
        private Destination   currentDestination;
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
            if (this.isTravelling || this.currentDestination != Destination.None) return;
            this.currentDestination = Destination.NewGame;
            this.isTravelling       = true;

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

        public void TravelToOptions()
        {
            if (this.isTravelling || this.currentDestination != Destination.None) return;
            this.currentDestination = Destination.Options;
            this.isTravelling       = true;

            this.titleCanvas.SetActive(false);
            EventSystem.current?.SetSelectedGameObject(null);

            DOTween.Sequence()
                .SetTarget(this)
                .Append(this.cameraTransform.DOMove(this.optionsPose.position, this.travelDuration).SetEase(this.travelEase))
                .Join(this.cameraTransform.DORotateQuaternion(this.optionsPose.rotation, this.travelDuration).SetEase(this.travelEase))
                .OnComplete(() =>
                {
                    this.isTravelling = false;
                    this.optionsCanvas.SetActive(true);
                    this.optionsController.Open();
                });
        }

        public void TravelToLoadGame()
        {
            if (this.isTravelling || this.currentDestination != Destination.None) return;
            this.currentDestination = Destination.LoadGame;
            this.isTravelling       = true;

            this.titleCanvas.SetActive(false);
            EventSystem.current?.SetSelectedGameObject(null);

            DOTween.Sequence()
                .SetTarget(this)
                .Append(this.cameraTransform.DOMove(this.loadGamePose.position, this.travelDuration).SetEase(this.travelEase))
                .Join(this.cameraTransform.DORotateQuaternion(this.loadGamePose.rotation, this.travelDuration).SetEase(this.travelEase))
                .OnComplete(() =>
                {
                    this.isTravelling = false;
                    this.loadGameCanvas.SetActive(true);
                    this.mainMenuController.OpenLoadGameList();
                });
        }

        /// <summary>
        /// The Load Game panel owns Confirm/Cancel itself (list vs confirm sub-state via
        /// SaveSlotNavigator), so OnCancel ignores Destination.LoadGame entirely -- this is
        /// called instead once MainMenuController's navigator fully closes (backed all the
        /// way out, not a slot being loaded).
        /// </summary>
        public void TravelBackFromLoadGame()
        {
            if (this.currentDestination != Destination.LoadGame || this.isTravelling) return;
            TravelBack();
        }

        private void OnCancel(InputAction.CallbackContext _)
        {
            if (this.currentDestination == Destination.None
                || this.currentDestination == Destination.LoadGame
                || this.isTravelling) return;
            TravelBack();
        }

        private void TravelBack()
        {
            Destination from = this.currentDestination;
            this.currentDestination = Destination.None;
            this.isTravelling       = true;

            switch (from)
            {
                case Destination.NewGame:
                    this.newGameCanvas.SetActive(false);
                    break;
                case Destination.Options:
                    this.optionsController.Close();
                    this.optionsCanvas.SetActive(false);
                    break;
                case Destination.LoadGame:
                    this.loadGameCanvas.SetActive(false);
                    break;
            }
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
