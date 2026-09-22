#nullable enable

using DG.Tweening;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using VContainer;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Navigation.CamaraSystem;
using CrimsonDraft.Navigation.Player.Movement;
using CrimsonDraft.Operators;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Pushables;

namespace CrimsonDraft.Navigation.Player
{
    public sealed class PlayerController : MonoBehaviour
    {
        private const int PlayerOperatorSlot = 0;

        [SerializeField] private Rigidbody rb       = null!;
        [SerializeField] private Animator  animator = null!;
        [SerializeField] private float walkSpeed         = 4f;
        [SerializeField] private float runSpeed          = 7f;
        [SerializeField] private float footOffset        = 1f;   // distancia del pivot del Rigidbody al suelo
        [SerializeField] private float navMeshTolerance  = 0.3f; // tolerancia horizontal para considerar "en NavMesh"

        [Header("Pushable Objects")]
        [SerializeField] private float pushDotThreshold   = 0.5f; // ~60 deg tolerance around the push axis
        [SerializeField] private float pushStrideInterval = 0.5f; // placeholder -- tiempo entre zancadas (tambien vale como la espera inicial antes de la primera)
        [SerializeField] private float pushStrideDistance = 0.5f; // placeholder -- distancia que avanza cada zancada, pendiente de pasada de feel

        [Header("Health Speed Steps (REmake-based)")]
        [SerializeField, Range(0f, 1f)] private float yellowCautionThreshold  = 0.75f;
        [SerializeField, Range(0f, 1f)] private float orangeCautionThreshold  = 0.50f;
        [SerializeField, Range(0f, 1f)] private float dangerThreshold         = 0.25f;
        [SerializeField, Range(0f, 1f)] private float yellowCautionSpeedRatio = 1.00f;
        [SerializeField, Range(0f, 1f)] private float orangeCautionSpeedRatio = 0.86f;
        [SerializeField, Range(0f, 1f)] private float dangerSpeedRatio        = 0.72f;

        private static readonly int ArmedHash   = Animator.StringToHash("Armed");
        private static readonly int IdleHash    = Animator.StringToHash("Idle");
        private static readonly int WalkHash    = Animator.StringToHash("Walk");
        private static readonly int RunHash     = Animator.StringToHash("Run");
        private static readonly int PushingHash = Animator.StringToHash("Pushing");
        private static readonly int GunTypeHash = Animator.StringToHash("GunType");
        private static readonly int SpeedHash       = Animator.StringToHash("Speed");
        private static readonly int HealthStateHash = Animator.StringToHash("HealthState");

        private IInputService         inputService         = null!;
        private IInventoryService     inventoryService     = null!;
        private IControlSchemeService controlSchemeService = null!;
        private IPlayerMovementStrategy modernStrategy  = null!;
        private IPlayerMovementStrategy classicStrategy = null!;
        private IOperatorRoster?      roster;
        private InputDevice?          lastDevice;
        private PushableObject?       touchingPushable;
        private Vector3               touchingPushDirection;
        private float                 pushStrideTimer;

        public bool IsAiming { get; private set; }

        // transform.position is the Rigidbody's pivot, not the ground — footOffset is the
        // vertical distance between them (see OnDrawGizmosSelected's "foot anchor" and
        // ResolveNavMeshDirection's sampleY). Anything that needs to place something at the
        // player's actual ground position (e.g. a dropped corpse) must use this, not
        // transform.position directly, or it ends up floating footOffset meters in the air.
        public Vector3 FootPosition => transform.position - new Vector3(0f, this.footOffset, 0f);

        [Inject]
        public void Construct(
            IInputService                  inputService,
            IInventoryService              inventoryService,
            ICameraRelativeMovementService cameraRelativeMovementService,
            IControlSchemeService          controlSchemeService,
            IOperatorRoster                roster)
        {
            this.inputService         = inputService;
            this.inventoryService     = inventoryService;
            this.controlSchemeService = controlSchemeService;
            this.roster               = roster;
            this.modernStrategy       = new ModernPlayerMovementStrategy(cameraRelativeMovementService);
            this.classicStrategy      = new ClassicPlayerMovementStrategy();
            this.inputService.Move.performed += OnMovePerformed;
        }

        private void OnDestroy()
        {
            if (this.inputService != null)
                this.inputService.Move.performed -= OnMovePerformed;
        }

        internal void SetAiming(bool value)
        {
            this.IsAiming = value;
        }

        private void OnMovePerformed(InputAction.CallbackContext ctx)
        {
            this.lastDevice = ctx.control.device;
        }

        // Called by PushableObjectSide (one per face) when the player enters/exits its trigger.
        // The push direction is fixed by the side, not recomputed here -- see
        // PushableObjectSide.Awake.
        internal void SetTouchingPushable(PushableObject pushable, Vector3 direction)
        {
            this.touchingPushable      = pushable;
            this.touchingPushDirection = direction;
        }

        internal void ClearTouchingPushable(PushableObject pushable)
        {
            if (this.touchingPushable == pushable)
                this.touchingPushable = null;
        }

        private void FixedUpdate()
        {
            var isArmed = this.inventoryService.GetEquippedWeaponIndex(PlayerOperatorSlot) >= 0;
            this.animator.SetBool(ArmedHash, isArmed);

            // GunType.Pistols is int 0, which also doubles as "nothing equipped" here --
            // fine since the Blend Tree/animator branch on GunType is only ever read while
            // Armed is also true.
            var activeWeapon = this.roster?[PlayerOperatorSlot].ActiveWeapon;
            this.animator.SetInteger(GunTypeHash, activeWeapon != null ? (int)activeWeapon.GunType : 0);

            var raw = this.inputService.Move.ReadValue<Vector2>();

            var strategy = this.controlSchemeService.CurrentScheme == ControlScheme.Classic
                ? this.classicStrategy
                : this.modernStrategy;

            // Always ticked (see IPlayerMovementStrategy) -- ModernPlayerMovementStrategy
            // depends on this running every frame, aiming or not.
            var result = strategy.Tick(transform, raw, this.lastDevice, this.IsAiming, Time.fixedDeltaTime);

            if (this.IsAiming)
            {
                this.rb.linearVelocity = Vector3.zero;
                this.pushStrideTimer = 0f;
                return;
            }

            if (result.Direction == Vector3.zero)
            {
                this.rb.linearVelocity = Vector3.zero;
                this.animator.SetTrigger(IdleHash);
                this.animator.SetFloat(SpeedHash, 0f);
                this.animator.SetBool(PushingHash, false);
                this.pushStrideTimer = 0f;
                return;
            }

            if (this.TryResolvePush(result.Direction, out var pushable, out var pushAxis))
            {
                // Locked in push mode: normal movement (and the sprint/health-speed path below)
                // is fully suppressed for as long as this holds -- the player can only move
                // together with the box, in discrete strides, never independently of it.
                this.pushStrideTimer += Time.fixedDeltaTime;
                transform.forward = pushAxis;
                this.animator.SetBool(PushingHash, true);
                this.rb.linearVelocity = Vector3.zero;

                if (this.pushStrideTimer < this.pushStrideInterval)
                {
                    // Between strides (this also covers the initial windup before the first
                    // stride): straining against it, box hasn't budged yet this stride.
                    this.animator.SetTrigger(IdleHash);
                    return;
                }

                this.pushStrideTimer = 0f;
                bool moved = pushable.TryStep(pushAxis, this.pushStrideDistance);
                this.animator.SetTrigger(moved ? WalkHash : IdleHash);

                // Tween the player the exact same distance and the exact same duration as the
                // box (read from the box, not a separately-tuned value here) -- keeps them glued
                // together with no gap a mismatch between two independently-driven bodies could
                // open. rb.DOMove (not a plain transform tween) keeps this MovePosition-safe for
                // the player's still-active, non-kinematic Rigidbody.
                if (moved)
                {
                    this.rb.DOKill();
                    this.rb.DOMove(this.rb.position + pushAxis * this.pushStrideDistance, pushable.StrideTweenDuration).SetEase(Ease.OutQuad);
                }
                return;
            }
            this.pushStrideTimer = 0f;
            this.animator.SetBool(PushingHash, false);

            var isSprinting     = this.inputService.Sprint.IsPressed() && result.AllowSprint;
            var speedMultiplier = this.GetSpeedMultiplier();
            this.animator.SetInteger(HealthStateHash, this.GetHealthStateTier());
            var speed           = (isSprinting ? this.runSpeed : this.walkSpeed) * speedMultiplier;

            this.animator.SetTrigger(isSprinting ? RunHash : WalkHash);
            this.animator.SetFloat(SpeedHash, isSprinting ? 2f : 1f);

            var resolvedDir = ResolveNavMeshDirection(result.Direction, speed);
            if (resolvedDir == Vector3.zero)
            {
                this.rb.linearVelocity = Vector3.zero;
                this.animator.SetTrigger(IdleHash);
                return;
            }

            this.rb.linearVelocity = resolvedDir * speed;
        }

        // Contact-only activation: no dedicated input, no IInteractable raycast+button path.
        // touchingPushable/touchingPushDirection come from PushableObjectSide's trigger, not a
        // physics contact or a probe -- moveDir only gates *whether* this counts as pushing into
        // it versus grazing past it tangentially.
        private bool TryResolvePush(Vector3 moveDir, out PushableObject pushable, out Vector3 axis)
        {
            pushable = this.touchingPushable!;
            axis     = this.touchingPushDirection;

            if (this.touchingPushable == null)
                return false;

            return Vector3.Dot(moveDir, axis) >= this.pushDotThreshold;
        }

        private Vector3 ResolveNavMeshDirection(Vector3 moveDir, float speed)
        {
            float   step    = speed * Time.fixedDeltaTime;
            Vector3 origin  = this.rb.position;
            float   sampleY = origin.y - this.footOffset;

            Vector3 next = new Vector3(origin.x + moveDir.x * step, sampleY, origin.z + moveDir.z * step);
            if (NavMesh.SamplePosition(next, out _, this.navMeshTolerance, NavMesh.AllAreas))
                return moveDir;

            Vector3 xOnly = new Vector3(origin.x + moveDir.x * step, sampleY, origin.z);
            if (NavMesh.SamplePosition(xOnly, out _, this.navMeshTolerance, NavMesh.AllAreas))
                // Keep moveDir's original per-axis magnitude here -- renormalizing to a unit
                // vector would boost the player back up to full speed on every NavMesh edge,
                // which is the "runs faster and slides at edges" bug.
                return new Vector3(moveDir.x, 0f, 0f);

            Vector3 zOnly = new Vector3(origin.x, sampleY, origin.z + moveDir.z * step);
            if (NavMesh.SamplePosition(zOnly, out _, this.navMeshTolerance, NavMesh.AllAreas))
                return new Vector3(0f, 0f, moveDir.z);

            return Vector3.zero;
        }

        private float GetSpeedMultiplier()
        {
            if (this.roster == null) return 1f;

            float lowestHpRatio = 1f;
            for (int i = 0; i < this.roster.Count; i++)
            {
                OperatorRuntime op = this.roster[i];
                if (!op.IsPresent || !op.IsAlive) continue;
                if (op.HpRatio < lowestHpRatio)
                    lowestHpRatio = op.HpRatio;
            }

            if (lowestHpRatio <= this.dangerThreshold)        return this.dangerSpeedRatio;
            if (lowestHpRatio <= this.orangeCautionThreshold) return this.orangeCautionSpeedRatio;
            if (lowestHpRatio <= this.yellowCautionThreshold) return this.yellowCautionSpeedRatio;
            return 1f; // Fine
        }

        // Mirrors GetSpeedMultiplier's thresholds but reports the discrete tier for the
        // Animator's Health Overlay layer (0=Normal,1=Yellow,2=Orange,3=Danger) instead of
        // a speed ratio. Kept as its own read-only pass over the roster rather than folded
        // into GetSpeedMultiplier, so existing speed behavior stays untouched.
        private int GetHealthStateTier()
        {
            if (this.roster == null) return 0;

            float lowestHpRatio = 1f;
            for (int i = 0; i < this.roster.Count; i++)
            {
                OperatorRuntime op = this.roster[i];
                if (!op.IsPresent || !op.IsAlive) continue;
                if (op.HpRatio < lowestHpRatio)
                    lowestHpRatio = op.HpRatio;
            }

            if (lowestHpRatio <= this.dangerThreshold)        return 3;
            if (lowestHpRatio <= this.orangeCautionThreshold) return 2;
            if (lowestHpRatio <= this.yellowCautionThreshold) return 1;
            return 0;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 foot = transform.position - new Vector3(0f, this.footOffset, 0f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(foot, 0.08f);

            bool onNavMesh = NavMesh.SamplePosition(foot, out _, this.navMeshTolerance, NavMesh.AllAreas);
            Gizmos.color = onNavMesh ? new Color(0f, 1f, 0f, 0.25f) : new Color(1f, 0f, 0f, 0.25f);
            Gizmos.DrawSphere(foot, this.navMeshTolerance);
        }
    }
}
