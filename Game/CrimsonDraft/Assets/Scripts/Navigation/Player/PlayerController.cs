#nullable enable

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
        [SerializeField] private CapsuleCollider? bodyCollider;                 // player body; defaults to the one on this object
        [SerializeField] private LayerMask pushableMask;                         // defaults to the "Pushable" layer
        [SerializeField] private float pushContactSkin       = 0.05f;           // gap still counted as touching a pushable
        [SerializeField] private float pushCornerMargin      = PushDetector.DefaultCornerMargin;      // RE2: 128 u
        [SerializeField] private float pushProbeRadiusFactor = PushDetector.DefaultProbeRadiusFactor; // RE2: 1.2 x half-width
        [SerializeField] private float pushProbeExtra        = PushDetector.DefaultProbeExtra;        // RE2: + 128 u
        [SerializeField] private float pushFloorTolerance    = 0.5f;            // feet vs pushable base: same floor
        [SerializeField] private float pushStartDelay        = 0.2f;            // RE2 (PC): 6 frames of sustained contact at 30 fps
        [SerializeField] private float pushSpeed             = 0.9f;            // placeholder -- velocidad de empuje, pendiente de pasada de feel
        [SerializeField] private float pushTurnSpeed         = 540f;            // deg/s while squaring up to the push axis

        [Header("Bite FX")]
        [Tooltip("Efecto de sangre instanciado en cada impacto del mordisco mientras está agarrado.")]
        [SerializeField] private GameObject? biteFxPrefab;
        [Tooltip("Punto desde donde sale la sangre del mordisco. Si está vacío se usa el hueso del cuello del Animator.")]
        [SerializeField] private Transform?  biteFxPoint;

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
        private static readonly int GrabHash        = Animator.StringToHash("Grab");
        private static readonly int GrabbedHash     = Animator.StringToHash("Grabbed");
        private static readonly int GrabbedHurtHash = Animator.StringToHash("GrabbedHurt");

        private IInputService         inputService         = null!;
        private IInventoryService     inventoryService     = null!;
        private IControlSchemeService controlSchemeService = null!;
        private IPlayerMovementStrategy modernStrategy  = null!;
        private IPlayerMovementStrategy classicStrategy = null!;
        private IOperatorRoster?      roster;
        private InputDevice?          lastDevice;
        private readonly Collider[]   pushCandidates = new Collider[8];
        private float                 pushHoldTimer;
        private Transform?            grabbedBy;
        private int                   healthOverlayLayerIndex = -1;

        public bool IsAiming { get; private set; }
        public bool IsGrabbed => this.grabbedBy != null;

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

        private void Awake()
        {
            this.healthOverlayLayerIndex = this.animator.GetLayerIndex("Health Overlay");

            if (this.bodyCollider == null)
                this.bodyCollider = GetComponent<CapsuleCollider>();
            if (this.pushableMask == 0)
                this.pushableMask = LayerMask.GetMask("Pushable");
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

        // Un solo enemigo puede sostener al jugador a la vez.
        public bool TryBeginGrab(Transform grabber)
        {
            if (this.grabbedBy != null) return false;
            this.grabbedBy = grabber;

            // GrabbedInto -> GrabbedLoop (clips del Horror Template retargeteados al rig Soldier).
            // El Health Overlay es un layer Override de cuerpo completo, así que se silencia mientras
            // dura el agarre para que la pose de herido no tape la animación (igual que al apuntar).
            this.animator.SetBool(GrabbedHash, true);
            this.animator.SetTrigger(GrabHash);
            if (this.healthOverlayLayerIndex >= 0)
                this.animator.SetLayerWeight(this.healthOverlayLayerIndex, 0f);
            return true;
        }

        public void EndGrab(Transform grabber)
        {
            if (this.grabbedBy != grabber) return;
            this.grabbedBy = null;

            this.animator.ResetTrigger(GrabHash);
            this.animator.SetBool(GrabbedHash, false);
            if (this.healthOverlayLayerIndex >= 0 && !this.IsAiming)
                this.animator.SetLayerWeight(this.healthOverlayLayerIndex, 1f);
        }

        // Impacto del mordisco mientras está agarrado: GrabbedLoop -> GrabbedHurt -> GrabbedLoop.
        public void NotifyBitten()
        {
            if (this.grabbedBy == null) return;
            this.animator.SetTrigger(GrabbedHurtHash);
            this.SpawnBiteFx(this.grabbedBy);
        }

        private void SpawnBiteFx(Transform grabber)
        {
            if (this.biteFxPrefab == null) return;

            var point = this.biteFxPoint != null
                ? this.biteFxPoint
                : this.animator.isHuman ? this.animator.GetBoneTransform(HumanBodyBones.Neck) : null;
            var fxPos = point != null ? point.position : transform.position + Vector3.up * 0.6f;

            // Corrido hacia el lado del enemigo, que es donde muerde.
            var toward = grabber.position - transform.position;
            toward.y   = 0f;
            if (toward.sqrMagnitude > 0.0001f)
                fxPos += toward.normalized * 0.1f;

            Instantiate(this.biteFxPrefab, fxPos, this.biteFxPrefab.transform.rotation);
        }

        private void OnMovePerformed(InputAction.CallbackContext ctx)
        {
            this.lastDevice = ctx.control.device;
        }

        private void FixedUpdate()
        {
            var isArmed = this.inventoryService.HasEquippedWeapon(PlayerOperatorSlot);
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

            if (this.grabbedBy != null)
            {
                this.rb.linearVelocity = Vector3.zero;
                // Las animaciones Grabbed* manejan la pose; no dejar triggers de locomoción
                // pendientes que se consuman al soltarse (GrabbedRelease -> Exit -> Start).
                this.animator.ResetTrigger(WalkHash);
                this.animator.ResetTrigger(RunHash);
                this.animator.ResetTrigger(IdleHash);
                this.animator.SetFloat(SpeedHash, 0f);
                this.ResetPush();

                var toGrabber = this.grabbedBy.position - transform.position;
                toGrabber.y = 0f;
                if (toGrabber.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(toGrabber);
                return;
            }

            if (this.IsAiming)
            {
                this.rb.linearVelocity = Vector3.zero;
                this.ResetPush();
                return;
            }

            if (result.Direction == Vector3.zero)
            {
                this.rb.linearVelocity = Vector3.zero;
                this.animator.SetTrigger(IdleHash);
                this.animator.SetFloat(SpeedHash, 0f);
                this.ResetPush();
                return;
            }

            if (this.TryResolvePush(result.Direction, out var pushable, out var pushAxis))
            {
                // Locked against the prop: normal movement (and the sprint/health-speed path below)
                // is suppressed for as long as this holds. RE2 sequence (PC 0x4CC320 / 0x4DB810):
                // hold contact for pushStartDelay, square up to the axis, then push continuously.
                this.rb.linearVelocity = Vector3.zero;
                this.pushHoldTimer += Time.fixedDeltaTime;

                if (this.pushHoldTimer < this.pushStartDelay)
                {
                    // Leaning on it: brushing past a prop never moves it, only a sustained push does.
                    this.animator.SetBool(PushingHash, false);
                    this.animator.SetTrigger(IdleHash);
                    return;
                }

                this.animator.SetBool(PushingHash, true);

                var squared = Quaternion.LookRotation(pushAxis);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, squared, this.pushTurnSpeed * Time.fixedDeltaTime);
                if (Quaternion.Angle(transform.rotation, squared) > 1f)
                {
                    this.animator.SetTrigger(IdleHash);
                    return;
                }

                if (!pushable.TryMove(pushAxis, this.pushSpeed * Time.fixedDeltaTime, this.bodyCollider))
                {
                    // Blocked: drop out of the push state (RE2 resets its counter). Still pushing
                    // restarts the delay, so a blocked prop is retried, not strained against forever.
                    this.ResetPush();
                    this.animator.SetTrigger(IdleHash);
                    return;
                }

                // The prop already moved this step; the player follows at the same speed and its
                // capsule stays against the face through normal physics contact.
                this.rb.linearVelocity = pushAxis * this.pushSpeed;
                this.animator.SetTrigger(WalkHash);
                return;
            }
            this.ResetPush();

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

        private void ResetPush()
        {
            this.pushHoldTimer = 0f;
            this.animator.SetBool(PushingHash, false);
        }

        // Contact-only activation: no dedicated input, no IInteractable raycast+button path.
        // RE2 rule (see PushDetector): exactly one pushable touched, on the player's floor, with the
        // player square to one of its faces and moving into it.
        private bool TryResolvePush(Vector3 moveDir, out PushableObject pushable, out Vector3 axis)
        {
            pushable = null!;
            axis     = Vector3.zero;

            if (this.bodyCollider == null)
                return false;

            Vector3 scale  = this.bodyCollider.transform.lossyScale;
            float   radius = this.bodyCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float   reach  = radius + this.pushContactSkin;

            // Candidates: anything on the pushable layer within the body capsule grown by the skin.
            Vector3 center     = this.bodyCollider.transform.TransformPoint(this.bodyCollider.center);
            float   halfHeight = this.bodyCollider.height * 0.5f * Mathf.Abs(scale.y);
            float   segment    = Mathf.Max(0f, halfHeight - radius);
            int count = Physics.OverlapCapsuleNonAlloc(
                center + Vector3.up * segment, center - Vector3.up * segment, reach,
                this.pushCandidates, this.pushableMask, QueryTriggerInteraction.Ignore);

            PushableObject? touched = null;
            for (int i = 0; i < count; i++)
            {
                var candidate = this.pushCandidates[i].GetComponentInParent<PushableObject>();
                if (candidate == null || candidate == touched) continue;
                if (!PushDetector.IsTouching(this.rb.position, radius, candidate.Bounds, this.pushContactSkin)) continue;

                // RE2 only pushes when exactly one object is touched: never two at once, and
                // never a box wedged against another one.
                if (touched != null)
                    return false;
                touched = candidate;
            }

            if (touched == null)
                return false;

            Bounds bounds = touched.Bounds;
            if (!PushDetector.IsSameFloor(this.FootPosition.y, bounds, this.pushFloorTolerance))
                return false;

            float probe = PushDetector.ProbeDistance(radius, this.pushProbeRadiusFactor, this.pushProbeExtra);
            if (!PushDetector.TryResolveAxis(this.rb.position, moveDir, bounds, this.pushCornerMargin, probe, out axis))
                return false;

            pushable = touched;
            return true;
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
