#nullable enable

using UnityEngine;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Navigation.Player;

namespace CrimsonDraft.Navigation.Enemy
{
    /// <summary>
    /// Gira la cabeza hacia el jugador mientras el enemigo está tirado y vivo (crawler dormido o
    /// trampa). Tirado boca abajo, girar la cabeza es rotar alrededor del eje del cuello (que va
    /// a lo largo del piso): la cara pasa de mirar el piso a mirar hacia el costado del jugador.
    /// El ángulo es cuánto está el jugador a izquierda/derecha del frente del cuerpo, limitado a
    /// ±<see cref="maxYaw"/> y suavizado. Se aplica en LateUpdate sobre la pose que acaba de
    /// escribir el Animator, así que nunca se acumula; si el jugador queda fuera del límite, se
    /// queda mirando al tope. Un cadáver colocado que todavía no despertó también mira (inquieta
    /// más); uno muerto de verdad, uno que se está levantando o uno agarrando no: ahí manda la
    /// animación. EnemyBootstrap le inyecta el jugador.
    /// </summary>
    [RequireComponent(typeof(EnemyNavAgent))]
    public sealed class EnemyHeadLook : MonoBehaviour
    {
        private const float NeckShare = 0.6f; // el resto lo gira la cabeza, para que no parezca una bisagra

        [Tooltip("Giro máximo del cuello, a cada lado, respecto del frente del cuerpo (grados).")]
        [SerializeField, Range(0f, 180f)] private float maxYaw = 70f;
        [Tooltip("Velocidad del giro (grados por segundo).")]
        [SerializeField, Min(1f)] private float turnSpeed = 90f;
        [Tooltip("Solo mira mientras no detectó al jugador (crawler dormido). Desactivar para que mire también estando alerta.")]
        [SerializeField] private bool onlyWhileIdle = true;

        private EnemyNavAgent     agent = null!;
        private PlayerController? player;
        private Transform?        neck;
        private Transform?        head;
        private float             currentYaw;

        public void Construct(PlayerController playerController) => this.player = playerController;

        /// <summary>Yaw (degrees, around world up) that points <paramref name="bodyForward"/> at
        /// <paramref name="toTarget"/>, clamped to ±<paramref name="maxYaw"/>.</summary>
        public static float TargetYaw(Vector3 bodyForward, Vector3 toTarget, float maxYaw)
        {
            bodyForward.y = 0f;
            toTarget.y    = 0f;
            if (bodyForward.sqrMagnitude < 0.0001f || toTarget.sqrMagnitude < 0.0001f) return 0f;
            return Mathf.Clamp(Vector3.SignedAngle(bodyForward, toTarget, Vector3.up), -maxYaw, maxYaw);
        }

        private void Awake()
        {
            this.agent = GetComponent<EnemyNavAgent>();
            var animator = GetComponent<Animator>();
            this.neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            this.head = animator.GetBoneTransform(HumanBodyBones.Head);
        }

        private void OnEnable() => this.currentYaw = 0f; // reactivar la sala reinicia el Animator

        /// <summary>Sign that makes a positive turn around <paramref name="neckAxis"/> swing a face
        /// pointing along <paramref name="face"/> toward <paramref name="bodyRight"/> (the side a
        /// positive <see cref="TargetYaw"/> points to).</summary>
        public static float TurnSign(Vector3 neckAxis, Vector3 face, Vector3 bodyRight)
            => Vector3.Dot(Quaternion.AngleAxis(1f, neckAxis) * face, bodyRight) >= Vector3.Dot(face, bodyRight) ? 1f : -1f;

        private void LateUpdate()
        {
            if (this.neck == null || this.head == null) return;

            var dt = Time.deltaTime * this.agent.TimeScale;
            if (dt <= 0f && this.currentYaw == 0f) return;

            var target = IsWatching() && this.player != null
                ? TargetYaw(transform.forward, this.player.transform.position - transform.position, this.maxYaw)
                : 0f;
            this.currentYaw = Mathf.MoveTowards(this.currentYaw, target, this.turnSpeed * dt);
            if (Mathf.Approximately(this.currentYaw, 0f)) return;

            // Eje del cuello en la pose de este frame; la cara de un cuerpo tirado mira al piso.
            var axis = this.head.position - this.neck.position;
            if (axis.sqrMagnitude < 0.0001f) return;
            axis.Normalize();
            var angle = this.currentYaw * TurnSign(axis, Vector3.down, transform.right);

            Rotate(this.neck, axis, angle * NeckShare);
            Rotate(this.head, axis, angle * (1f - NeckShare));
        }

        private bool IsWatching()
        {
            if (this.agent.IsFeigningDeath) return true; // "muerto" colocado: te sigue con la cabeza igual
            if (this.agent.IsDead || this.agent.IsRising) return false;
            if (this.agent.State == EnemyAlertState.Attack) return false;
            return !this.onlyWhileIdle || this.agent.State == EnemyAlertState.Idle;
        }

        private static void Rotate(Transform bone, Vector3 axis, float angle)
            => bone.rotation = Quaternion.AngleAxis(angle, axis) * bone.rotation;
    }
}
