#nullable enable

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using VContainer;
using CrimsonDraft.Infrastructure.Input;

namespace CrimsonDraft.Navigation.Player
{
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private Rigidbody rb       = null!;
        [SerializeField] private Animator  animator = null!;
        [SerializeField] private float walkSpeed = 4f;
        [SerializeField] private float runSpeed  = 7f;

        private static readonly int SpeedHash    = Animator.StringToHash("Speed");
        private static readonly int IsAimingHash = Animator.StringToHash("IsAiming");

        private IInputService inputService = null!;
        private InputDevice?  lastDevice;

        public bool IsAiming { get; private set; }

        [Inject]
        public void Construct(IInputService inputService)
        {
            this.inputService = inputService;
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
                return;
            }

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
