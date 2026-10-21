#nullable enable

using System.Collections;
using UnityEngine;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Navigation.Interactables
{
    /// <summary>Lives on the same GameObject as the beeper detector's trigger BoxCollider
    /// (BeeperReceiverInteractable), so it reuses that trigger's OnTriggerEnter/Exit instead of
    /// adding a second collider. Stays fully inert (screen keeps its default/original material,
    /// glow off) until Activate() is called -- e.g. by BeeperDoorMechanism once the room's
    /// beeper is picked up. Once active, swaps the radio screen between insideMaterial (player
    /// in range) and outsideMaterial (player out of range) and drives a small glow Light on the
    /// screen -- steady when inside, blinking when outside. PlayAcceptedFlash briefly overrides
    /// both with a "code accepted" material/color before resuming that cycle.
    ///
    /// "In range" only counts while a living operator carries the beeper item (polled from the
    /// inventory): without it the radio shows the out-of-range look even with the player inside
    /// the trigger. Bind() is called by BeeperReceiverInteractable.Construct. A radio without a
    /// BeeperDoorMechanism beside it switches on at Bind(); one with a mechanism waits for its
    /// Activate() call.</summary>
    public sealed class BeeperRadioIndicator : MonoBehaviour
    {
        [Header("Screen")]
        [SerializeField] private Renderer screenRenderer     = null!;
        [SerializeField] private int      screenMaterialSlot = 1;
        [SerializeField] private Material insideMaterial     = null!;
        [SerializeField] private Material outsideMaterial    = null!;

        [Header("Glow")]
        [SerializeField] private Light  glowLight          = null!;
        [SerializeField] private Color  insideGlowColor    = new(0.3f, 1f, 0.4f);
        [SerializeField] private Color  outsideGlowColor   = new(1f, 0.15f, 0.1f);
        [SerializeField] private Color  acceptedGlowColor  = new(0.2f, 0.5f, 1f);
        [SerializeField] private float  glowIntensity      = 1.2f;
        [SerializeField] private float  blinkInterval      = 0.4f;

        [Header("Beeper")]
        [SerializeField] private string beeperItemId       = "KI_21";

        private static readonly int LightnessMultiplier = Shader.PropertyToID("_LightnessMultiplier");

        private MaterialPropertyBlock? block;
        private bool       active; // set by Activate() -- before that the screen/light are left untouched
        private bool       playerInRange;
        private bool       hasBeeper;
        private float      beeperPollTimer;
        private IInventoryService? inventory;
        private bool       blinkOn = true;
        private float      blinkTimer;
        private Coroutine? flashRoutine;

        void Awake()
        {
            this.block = new MaterialPropertyBlock();

            // Stays dark until Activate() -- otherwise the light would shine at its authored
            // intensity from scene load, before the player has any reason to notice this radio.
            if (this.glowLight != null) this.glowLight.enabled = false;
        }

        private const float BeeperPollInterval = 0.25f;

        // Player is inside the trigger AND carries the beeper -- what the screen/light follow.
        private bool InRange => this.playerInRange && this.hasBeeper;

        public void Bind(IInventoryService inventoryService)
        {
            this.inventory = inventoryService;
            this.hasBeeper = inventoryService.HasItem(this.beeperItemId);

            if (GetComponent<BeeperDoorMechanism>() == null)
                Activate();
        }

        void OnEnable()
        {
            this.blinkOn    = true;
            this.blinkTimer = 0f;
            if (this.active && this.flashRoutine == null) ApplyState();
        }

        // Switches the indicator on for the first time -- called once the room's beeper is
        // picked up. Before this, the screen keeps whatever material it was authored with.
        public void Activate()
        {
            if (this.active) return;
            this.active = true;
            ApplyState();
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            this.playerInRange = true;
            if (this.active && this.flashRoutine == null) ApplyState();
        }

        void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            this.playerInRange = false;
            if (this.active && this.flashRoutine == null) ApplyState();
        }

        void Update()
        {
            if (!this.active) return;

            this.beeperPollTimer += Time.deltaTime;
            if (this.beeperPollTimer >= BeeperPollInterval)
            {
                this.beeperPollTimer = 0f;
                bool carried = this.inventory != null && this.inventory.HasItem(this.beeperItemId);
                if (carried != this.hasBeeper)
                {
                    this.hasBeeper = carried;
                    if (this.flashRoutine == null) ApplyState();
                }
            }

            if (InRange || this.flashRoutine != null) return;

            this.blinkTimer += Time.deltaTime;
            if (this.blinkTimer < this.blinkInterval) return;

            this.blinkTimer = 0f;
            this.blinkOn    = !this.blinkOn;
            ApplyGlow();
        }

        // Briefly shows flashMaterial at full brightness (e.g. a blue "accepted" screen) for
        // duration seconds, then resumes the normal in/out-of-range cycle.
        public void PlayAcceptedFlash(Material flashMaterial, float duration)
        {
            if (!this.active) return;
            if (this.flashRoutine != null) StopCoroutine(this.flashRoutine);
            this.flashRoutine = StartCoroutine(FlashRoutine(flashMaterial, duration));
        }

        private IEnumerator FlashRoutine(Material flashMaterial, float duration)
        {
            SetScreenMaterial(flashMaterial);

            if (this.glowLight != null)
            {
                this.glowLight.enabled   = true;
                this.glowLight.color     = this.acceptedGlowColor;
                this.glowLight.intensity = this.glowIntensity;
            }

            SetLightness(1f);

            yield return new WaitForSeconds(duration);

            this.flashRoutine = null;
            this.blinkOn      = true;
            this.blinkTimer   = 0f;
            ApplyState();
        }

        void ApplyState()
        {
            SetScreenMaterial(InRange ? this.insideMaterial : this.outsideMaterial);
            this.blinkOn    = true;
            this.blinkTimer = 0f;
            ApplyGlow();
        }

        void SetScreenMaterial(Material material)
        {
            if (this.screenRenderer == null) return;

            var mats = this.screenRenderer.sharedMaterials;
            if (this.screenMaterialSlot < 0 || this.screenMaterialSlot >= mats.Length) return;

            mats[this.screenMaterialSlot] = material;
            this.screenRenderer.sharedMaterials = mats;
        }

        void ApplyGlow()
        {
            float lit = InRange || this.blinkOn ? 1f : 0.1f;

            if (this.glowLight != null)
            {
                this.glowLight.enabled   = true;
                this.glowLight.color     = InRange ? this.insideGlowColor : this.outsideGlowColor;
                this.glowLight.intensity = this.glowIntensity * lit;
            }

            SetLightness(lit);
        }

        void SetLightness(float lit)
        {
            if (this.screenRenderer == null || this.block == null) return;

            this.screenRenderer.GetPropertyBlock(this.block, this.screenMaterialSlot);
            this.block.SetFloat(LightnessMultiplier, lit);
            this.screenRenderer.SetPropertyBlock(this.block, this.screenMaterialSlot);
        }
    }
}
