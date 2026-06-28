#nullable enable

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using VContainer;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Navigation.Enemy;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Navigation.Player
{
    public sealed class PlayerController : MonoBehaviour
    {
        private const int PlayerOperatorSlot = 0;

        [SerializeField] private Rigidbody rb = null!;
        [SerializeField] private Animator animator = null!;
        [SerializeField] private float walkSpeed = 4f;
        [SerializeField] private float runSpeed = 7f;
        [SerializeField] private float shootRange = 8f;
        [SerializeField] private LayerMask enemyLayer;

        private static readonly int SpeedHash  = Animator.StringToHash("Speed");
        private static readonly int ArmedHash  = Animator.StringToHash("Armed");
        private static readonly int AimingHash = Animator.StringToHash("Aiming");

        private IInputService  inputService  = null!;
        private OperatorRuntime playerOperator = null!;
        private InputDevice?    lastDevice;
        private bool            isAiming;

        [Inject]
        public void Construct(IInputService inputService, IOperatorRoster roster)
        {
            this.inputService = inputService;
            this.inputService.Move.performed += OnMovePerformed;

            this.inputService.Aim.started   += OnAimStarted;
            this.inputService.Aim.canceled  += OnAimCanceled;
            this.inputService.Shoot.performed += OnShootPerformed;

            roster.EnsureInitialized();
            this.playerOperator = roster[PlayerOperatorSlot];
            this.playerOperator.ActiveWeaponChanged += OnActiveWeaponChanged;
            this.animator.SetBool(ArmedHash, this.playerOperator.ActiveWeapon != null);
        }

        private void OnDestroy()
        {
            if (this.inputService != null)
            {
                this.inputService.Move.performed -= OnMovePerformed;
                this.inputService.Aim.started    -= OnAimStarted;
                this.inputService.Aim.canceled   -= OnAimCanceled;
                this.inputService.Shoot.performed -= OnShootPerformed;
            }

            if (this.playerOperator != null)
                this.playerOperator.ActiveWeaponChanged -= OnActiveWeaponChanged;
        }

        private void OnActiveWeaponChanged(IWeaponSlot? weapon)
            => this.animator.SetBool(ArmedHash, weapon != null);

        private void OnAimStarted(InputAction.CallbackContext ctx)
        {
            if (this.playerOperator.ActiveWeapon == null) return;
            this.isAiming = true;
            this.animator.SetBool(AimingHash, true);
        }

        private void OnAimCanceled(InputAction.CallbackContext ctx)
        {
            this.isAiming = false;
            this.animator.SetBool(AimingHash, false);
        }

        private void OnShootPerformed(InputAction.CallbackContext ctx)
        {
            if (!this.isAiming) return;

            if (!Physics.Raycast(transform.position, transform.forward, out var hit, this.shootRange, this.enemyLayer))
                return;

            if (hit.collider.TryGetComponent<EnemyNavAgent>(out var enemy))
                enemy.NotifyShot();
        }

        private void OnMovePerformed(InputAction.CallbackContext ctx)
        {
            this.lastDevice = ctx.control.device;
        }

        private void FixedUpdate()
        {
            var raw = this.inputService.Move.ReadValue<Vector2>();

            if (raw.sqrMagnitude < 0.01f)
            {
                this.rb.linearVelocity = Vector3.zero;
                this.animator.SetFloat(SpeedHash, 0f);
                return;
            }

            var direction = this.lastDevice is Gamepad
                ? raw.normalized
                : Quantize8Way(raw);

            var moveDir = new Vector3(direction.x, 0f, direction.y);
            transform.forward = moveDir;

            var isSprinting = this.inputService.Sprint.IsPressed();
            var speed       = isSprinting ? this.runSpeed  : this.walkSpeed;
            var animSpeed   = isSprinting ? 1f             : 0.5f;

            this.rb.linearVelocity = moveDir * speed;
            this.animator.SetFloat(SpeedHash, animSpeed);
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
