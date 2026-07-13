#nullable enable

using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using VContainer;
using CrimsonDraft.Audio;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Navigation.Player
{
    public sealed class PlayerController : MonoBehaviour, IPlayerMotion
    {
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

        private static readonly int SpeedHash    = Animator.StringToHash("Speed");
        private static readonly int IsAimingHash = Animator.StringToHash("IsAiming");

        private IInputService  inputService = null!;
        private IOperatorRoster? roster;
        private InputDevice?   lastDevice;

        public bool  IsAiming     { get; private set; }
        public bool  IsSprinting  { get; private set; }
        public float CurrentSpeed { get; private set; }

        [Inject]
        public void Construct(IInputService inputService, IOperatorRoster roster)
        {
            this.inputService = inputService;
            this.roster       = roster;
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
            this.animator.SetBool(IsAimingHash, value);
        }

        private void OnMovePerformed(InputAction.CallbackContext ctx)
        {
            this.lastDevice = ctx.control.device;
        }

        private void FixedUpdate()
        {
            if (this.IsAiming)
            {
                this.rb.linearVelocity = Vector3.zero;
                this.animator.SetFloat(SpeedHash, 0f);
                this.CurrentSpeed = 0f;
                this.IsSprinting  = false;
                return;
            }

            var raw = this.inputService.Move.ReadValue<Vector2>();

            if (raw.sqrMagnitude < 0.01f)
            {
                this.rb.linearVelocity = Vector3.zero;
                this.animator.SetFloat(SpeedHash, 0f);
                this.CurrentSpeed = 0f;
                this.IsSprinting  = false;
                return;
            }

            var direction = this.lastDevice is Gamepad
                ? raw.normalized
                : Quantize8Way(raw);

            var moveDir = new Vector3(direction.x, 0f, direction.y);
            transform.forward = moveDir;

            var isSprinting     = this.inputService.Sprint.IsPressed();
            var speedMultiplier = this.GetSpeedMultiplier();
            var speed           = (isSprinting ? this.runSpeed : this.walkSpeed) * speedMultiplier;
            var animSpeed       = isSprinting ? 1f : 0.5f;

            var resolvedDir = ResolveNavMeshDirection(moveDir, speed);
            if (resolvedDir == Vector3.zero)
            {
                this.rb.linearVelocity = Vector3.zero;
                this.animator.SetFloat(SpeedHash, 0f);
                this.CurrentSpeed = 0f;
                this.IsSprinting  = false;
                return;
            }

            this.rb.linearVelocity = resolvedDir * speed;
            this.animator.SetFloat(SpeedHash, animSpeed);
            this.CurrentSpeed = speed;
            this.IsSprinting  = isSprinting;
        }

        private Vector3 ResolveNavMeshDirection(Vector3 moveDir, float speed)
        {
            float   step    = speed * Time.fixedDeltaTime;
            Vector3 origin  = this.rb.position;
            float   sampleY = origin.y - this.footOffset;

            Vector3 next  = new Vector3(origin.x + moveDir.x * step, sampleY, origin.z + moveDir.z * step);
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

            // Foot anchor
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(foot, 0.08f);

            // NavMesh tolerance radius at foot level
            bool onNavMesh = NavMesh.SamplePosition(foot, out _, this.navMeshTolerance, NavMesh.AllAreas);
            Gizmos.color = onNavMesh ? new Color(0f, 1f, 0f, 0.25f) : new Color(1f, 0f, 0f, 0.25f);
            Gizmos.DrawSphere(foot, this.navMeshTolerance);
        }

        private static Vector2 Quantize8Way(Vector2 input)
        {
            return new Vector2(
                Mathf.Round(input.x),
                Mathf.Round(input.y)
            ).normalized;
        }
    }
}
