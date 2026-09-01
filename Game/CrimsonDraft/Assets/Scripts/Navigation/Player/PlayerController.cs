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

        [Header("Health Speed Steps (REmake-based)")]
        [SerializeField, Range(0f, 1f)] private float yellowCautionThreshold  = 0.75f;
        [SerializeField, Range(0f, 1f)] private float orangeCautionThreshold  = 0.50f;
        [SerializeField, Range(0f, 1f)] private float dangerThreshold         = 0.25f;
        [SerializeField, Range(0f, 1f)] private float yellowCautionSpeedRatio = 1.00f;
        [SerializeField, Range(0f, 1f)] private float orangeCautionSpeedRatio = 0.86f;
        [SerializeField, Range(0f, 1f)] private float dangerSpeedRatio        = 0.72f;

        private static readonly int ArmedHash = Animator.StringToHash("Armed");
        private static readonly int IdleHash  = Animator.StringToHash("Idle");
        private static readonly int WalkHash  = Animator.StringToHash("Walk");
        private static readonly int RunHash   = Animator.StringToHash("Run");

        private IInputService         inputService         = null!;
        private IInventoryService     inventoryService     = null!;
        private IControlSchemeService controlSchemeService = null!;
        private IPlayerMovementStrategy modernStrategy  = null!;
        private IPlayerMovementStrategy classicStrategy = null!;
        private IOperatorRoster?      roster;
        private InputDevice?          lastDevice;

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

        private void FixedUpdate()
        {
            var isArmed = this.inventoryService.GetEquippedWeaponIndex(PlayerOperatorSlot) >= 0;
            this.animator.SetBool(ArmedHash, isArmed);

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
                return;
            }

            if (result.Direction == Vector3.zero)
            {
                this.rb.linearVelocity = Vector3.zero;
                this.animator.SetTrigger(IdleHash);
                return;
            }

            var isSprinting     = this.inputService.Sprint.IsPressed() && result.AllowSprint;
            var speedMultiplier = this.GetSpeedMultiplier();
            var speed           = (isSprinting ? this.runSpeed : this.walkSpeed) * speedMultiplier;

            this.animator.SetTrigger(isSprinting ? RunHash : WalkHash);

            var resolvedDir = ResolveNavMeshDirection(result.Direction, speed);
            if (resolvedDir == Vector3.zero)
            {
                this.rb.linearVelocity = Vector3.zero;
                this.animator.SetTrigger(IdleHash);
                return;
            }

            this.rb.linearVelocity = resolvedDir * speed;
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
                return new Vector3(moveDir.x, 0f, 0f).normalized;

            Vector3 zOnly = new Vector3(origin.x, sampleY, origin.z + moveDir.z * step);
            if (NavMesh.SamplePosition(zOnly, out _, this.navMeshTolerance, NavMesh.AllAreas))
                return new Vector3(0f, 0f, moveDir.z).normalized;

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
