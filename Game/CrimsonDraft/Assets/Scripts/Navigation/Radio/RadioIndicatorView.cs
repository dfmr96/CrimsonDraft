#nullable enable

using UnityEngine;
using VContainer;
using CrimsonDraft.Infrastructure.UI;

namespace CrimsonDraft.Navigation.Radio
{
    /// <summary>
    /// HUD icon (bottom-left) shown while a radio call is waiting. It "rings": the icon wobbles
    /// while the call sound plays, then rests for <see cref="repeatDelay"/> and rings again, until
    /// the call is answered. It hides (and goes quiet) while the inventory is open, because that
    /// is where the call is answered.
    /// </summary>
    public sealed class RadioIndicatorView : MonoBehaviour
    {
        [SerializeField] private GameObject icon = null!;

        [Header("Ringing")]
        [SerializeField] private AK.Wwise.Event callEvent = new();
        [Tooltip("Pause between the end of one ring and the start of the next.")]
        [SerializeField] private float repeatDelay = 0.5f;
        [Tooltip("Peak tilt of the icon while it rings, in degrees.")]
        [SerializeField] private float wobbleAngle = 8f;
        [Tooltip("Wobble speed while ringing, in oscillations per second.")]
        [SerializeField] private float wobbleHz = 7f;

        // The stacked UI CRT camera post-processes the whole frame, world included, so the icon is
        // drawn the way the pause menu's Video tab draws its UI: through a dedicated camera into a
        // render texture that is composited over the screen. That gives the icon the UI's CRT look
        // without touching the world. Optional: null keeps the icon on its own canvas camera.
        [Header("UI-only CRT pass")]
        [SerializeField] private Camera?     uiCamera;
        [SerializeField] private GameObject? uiComposite;

        private IRadioConversationService? service;
        private Canvas? canvas;
        private Camera? stackedCamera;
        private bool  inventoryOpen;

        private bool  lastRinging;
        private bool  ringing;
        private bool  soundPlaying;
        private int   ringSession;
        private float nextRingTime;
        private float wobbleClock;

        [Inject]
        public void Construct(IRadioConversationService service) => this.service = service;

        private void Start()
        {
            this.canvas        = GetComponent<Canvas>();
            this.stackedCamera = this.canvas != null ? this.canvas.worldCamera : null;

            if (this.service == null) return;

            this.service.PendingChanged  += Refresh;
            this.service.InventoryOpened += OnInventoryOpened;
            this.service.InventoryClosed += OnInventoryClosed;
            Refresh();
        }

        private void OnDestroy()
        {
            StopRinging();
            SetUiOnlyPass(false);
            UiCameraGate.Release(this);
            if (this.service == null) return;

            this.service.PendingChanged  -= Refresh;
            this.service.InventoryOpened -= OnInventoryOpened;
            this.service.InventoryClosed -= OnInventoryClosed;
        }

        private void OnInventoryOpened() { this.inventoryOpen = true;  Refresh(); }
        private void OnInventoryClosed() { this.inventoryOpen = false; Refresh(); }

        private void Refresh()
        {
            this.lastRinging = this.service?.IsRinging ?? false;
            bool visible = this.lastRinging && !this.inventoryOpen;
            this.icon.SetActive(visible);
            SetUiOnlyPass(visible);
            bool ownPass = this.uiCamera != null && this.uiComposite != null;
            if (visible && !ownPass) UiCameraGate.Request(this);
            else                     UiCameraGate.Release(this);

            if (visible && !this.ringing) StartRinging();
            else if (!visible && this.ringing) StopRinging();
        }

        private void SetUiOnlyPass(bool on)
        {
            if (this.uiCamera == null || this.uiComposite == null || this.canvas == null) return;

            this.uiCamera.gameObject.SetActive(on);
            this.uiComposite.SetActive(on);
            this.canvas.worldCamera = on ? this.uiCamera : this.stackedCamera;
        }

        private void StartRinging()
        {
            this.ringing    = true;
            this.wobbleClock = 0f;
            Ring();
        }

        private void StopRinging()
        {
            if (!this.ringing) return;

            this.ringing = false;
            this.ringSession++;
            this.soundPlaying = false;
            this.callEvent.Stop(this.gameObject);
            this.icon.transform.localRotation = Quaternion.identity;
        }

        private void Ring()
        {
            int session = ++this.ringSession;
            this.soundPlaying = true;
            this.wobbleClock  = 0f;

            this.callEvent.Post(
                this.gameObject,
                (uint)AkCallbackType.AK_EndOfEvent,
                (cookie, type, info) =>
                {
                    // A stopped ring also fires its end callback; only the current one counts.
                    if (session == this.ringSession) this.soundPlaying = false;
                });
        }

        private void Update()
        {
            // Combat starting/ending changes whether the radio may ring without any event of ours.
            if (this.service != null && this.service.IsRinging != this.lastRinging) Refresh();

            if (!this.ringing) return;

            // Unscaled: the HUD keeps ringing while the game is paused (timeScale 0).
            float dt = Time.unscaledDeltaTime;

            if (this.soundPlaying)
            {
                this.wobbleClock += dt;
                float tilt = Mathf.Sin(this.wobbleClock * this.wobbleHz * Mathf.PI * 2f) * this.wobbleAngle;
                this.icon.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
                this.nextRingTime = Time.unscaledTime + this.repeatDelay;
                return;
            }

            this.icon.transform.localRotation = Quaternion.identity;
            if (Time.unscaledTime >= this.nextRingTime) Ring();
        }
    }
}
