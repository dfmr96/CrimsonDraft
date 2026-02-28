#nullable enable

using UnityEngine;
using VContainer;
using CrimsonDraft.Infrastructure.Input;

namespace CrimsonDraft.Navigation.Player
{
    public sealed class PlayerController : MonoBehaviour
    {
        private static readonly int FacingDirectionHash = Animator.StringToHash("FacingDirection");
        private static readonly int IsMovingHash        = Animator.StringToHash("IsMoving");

        [SerializeField] private Rigidbody2D rb = null!;
        [SerializeField] private Animator animator = null!;
        [SerializeField] private float moveSpeed = 4f;

        private IInputService inputService = null!;
        private FacingDirection facing = FacingDirection.Down;

        [Inject]
        public void Construct(IInputService inputService)
        {
            this.inputService = inputService;
        }

        private void FixedUpdate()
        {
            var raw = this.inputService.Move.ReadValue<Vector2>();
            var direction = QuantizeToCardinal(raw);

            this.rb.linearVelocity = direction * this.moveSpeed;

            var isMoving = direction != Vector2.zero;
            this.animator.SetBool(IsMovingHash, isMoving);

            if (isMoving)
                UpdateFacing(direction);
        }

        private static Vector2 QuantizeToCardinal(Vector2 input)
        {
            if (input.sqrMagnitude < 0.01f)
                return Vector2.zero;

            return Mathf.Abs(input.x) >= Mathf.Abs(input.y)
                ? new Vector2(Mathf.Sign(input.x), 0f)
                : new Vector2(0f, Mathf.Sign(input.y));
        }

        private void UpdateFacing(Vector2 direction)
        {
            this.facing = direction switch
            {
                { x: > 0 } => FacingDirection.Right,
                { x: < 0 } => FacingDirection.Left,
                { y: > 0 } => FacingDirection.Up,
                _           => FacingDirection.Down,
            };

            this.animator.SetInteger(FacingDirectionHash, (int)this.facing);
        }
    }
}
