#nullable enable

using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using VContainer;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Navigation.Player;

namespace CrimsonDraft.Navigation.Interactables
{
    // Locker that opens once its ItemSocketInteractable is completed: the door swings open, the
    // gem inside falls out onto the floor, and after a pause a rat jumps out at the player's face
    // (a scare, no damage), drops to the ground and runs off until it leaves the camera's view.
    //
    // Put this on the same GameObject as the socket. The sequence is started by the socket's
    // OnActivatedAfterDialogue, so it plays after the player has read the line. The player can't
    // act for the whole sequence. Re-entering the room with the socket already activated skips it
    // and snaps everything to the final state (open door, no rat, gem on the floor); a short
    // polling window right after enable covers that, since ItemSocketInteractable restores its
    // state without replaying events.
    //
    // The rat model faces -Z (Blender export), so it looks along -forward.
    public sealed class SocketLockerRatSequence : MonoBehaviour
    {
        [SerializeField] private ItemSocketInteractable socket = null!;

        [Header("Door")]
        [SerializeField] private Transform door = null!;
        [Tooltip("Local Z rotation (Euler, degrees) the door ends at when opened. X and Y keep their authored values.")]
        [SerializeField] private float doorOpenLocalZ = -142f;
        [SerializeField, Min(0f)] private float startDelay = 0.4f;
        [SerializeField, Min(0.01f)] private float doorDuration = 1f;

        [Header("Rat")]
        [Tooltip("Starts inactive; shown once the gem has fallen and the delay below has passed.")]
        [SerializeField] private GameObject rat = null!;
        [SerializeField] private Animator ratAnimator = null!;
        [Tooltip("Where the rat waits inside the locker.")]
        [SerializeField] private Transform ratStart = null!;
        [Tooltip("Pause between the gem landing and the rat showing up.")]
        [SerializeField, Min(0f)] private float ratDelay = 1f;
        [Tooltip("Pause between the rat appearing and jumping.")]
        [SerializeField, Min(0f)] private float peekDuration = 0.35f;
        [SerializeField, Min(0.05f)] private float leapDuration = 0.45f;
        [SerializeField, Min(0f)] private float leapArcHeight = 0.25f;
        [Tooltip("How long the rat stays on the face biting before it lets go.")]
        [SerializeField, Min(0f)] private float biteDuration = 1.4f;
        [Tooltip("Time between bites; each one replays the Attack animation and makes the player flinch.")]
        [SerializeField, Min(0.1f)] private float biteInterval = 0.4f;
        [Tooltip("Positional shake of the rat while it bites (metres).")]
        [SerializeField, Min(0f)] private float biteShake = 0.015f;
        [Tooltip("Rotational shake of the rat while it bites (degrees).")]
        [SerializeField, Min(0f)] private float biteJitter = 10f;
        [Tooltip("How far the rat hops away from the face when it lets go (metres).")]
        [SerializeField, Min(0f)] private float hopAwayDistance = 0.5f;
        [SerializeField, Min(0.05f)] private float dropDuration = 0.5f;
        [Tooltip("Pause on the floor before running.")]
        [SerializeField, Min(0f)] private float landPause = 0.3f;
        [SerializeField, Min(0.1f)] private float runSpeed = 3.5f;
        [Tooltip("Optional: run towards this point instead of picking the nearest way out of the camera's view.")]
        [SerializeField] private Transform? escapePoint;
        [Tooltip("Fires when the rat hits the player's face (sound, shake...).")]
        [SerializeField] private UnityEvent onImpact = new();

        [Header("Player")]
        [Tooltip("How far the player is startled back when the rat hits their face (metres).")]
        [SerializeField, Min(0f)] private float staggerDistance = 0.8f;
        [SerializeField, Min(0.05f)] private float staggerDuration = 0.45f;

        [Header("Audio (Wwise)")]
        [Tooltip("Metal door sound; posted on the door as it starts to open.")]
        [SerializeField] private AK.Wwise.Event doorOpenEvent = new();
        [Tooltip("Gem hitting the floor; posted on the gem when it lands.")]
        [SerializeField] private AK.Wwise.Event gemDropEvent = new();
        [Tooltip("Seconds before the gem lands at which its sound is posted, to make up for the latency.")]
        [SerializeField, Min(0f)] private float gemDropSoundLead = 0.04f;
        [Tooltip("Scare stinger; posted when the rat leaps at the player.")]
        [SerializeField] private AK.Wwise.Event stingerEvent = new();

        [Header("Gem")]
        [Tooltip("Inactive parent of the gem pickup; shown when the door has opened. The pickup itself keeps its own collected state.")]
        [SerializeField] private GameObject gemHolder = null!;
        [Tooltip("The gem pickup. Its pose in the prefab is where it sits inside the locker.")]
        [SerializeField] private Transform gem = null!;
        [SerializeField] private Transform gemLanding = null!;
        [Tooltip("Point of the door's opening (0-1) at which the gem starts to fall; it keeps falling while the door finishes opening.")]
        [SerializeField, Range(0f, 1f)] private float gemDropAtDoor = 0.15f;
        [Tooltip("How long the gem takes to fall out of the locker onto the floor.")]
        [SerializeField, Min(0.05f)] private float gemFlightDuration = 0.5f;
        [Tooltip("Small outward hop as it leaves the locker.")]
        [SerializeField, Min(0f)] private float gemArcHeight = 0.08f;

        private const float RestoreWindow = 1f;
        private const float FallGravity = 9.81f;
        private const string AttackTrigger = "Attack";
        private const string RunBool = "Run";

        private Vector3 doorStartLocalEuler;
        private float enabledAt;
        private bool played;
        private bool locked;
        private IInputService? input;
        private PlayerController? player;

        void Awake()
        {
            this.doorStartLocalEuler = this.door.localEulerAngles;
        }

        void OnEnable()
        {
            this.enabledAt = Time.time;
            this.socket.OnActivatedAfterDialogue.AddListener(Play);
        }

        void OnDisable()
        {
            this.socket.OnActivatedAfterDialogue.RemoveListener(Play);
            Unlock();
        }

        void Update()
        {
            if (this.played || Time.time - this.enabledAt > RestoreWindow) return;
            if (!this.socket.IsActivated) return;

            this.played = true;
            SnapToFinal();
        }

        private void Play()
        {
            if (this.played) return;
            this.played = true;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            Lock();
            try
            {
                if (this.startDelay > 0f)
                    yield return new WaitForSeconds(this.startDelay);

                yield return OpenDoor(); // also starts the gem's fall as the door begins to open
                if (this.ratDelay > 0f)
                    yield return new WaitForSeconds(this.ratDelay);

                yield return RatJump();
                yield return RatEscape();
            }
            finally
            {
                this.rat.SetActive(false);
                Unlock();
            }
        }

        private void SnapToFinal()
        {
            SetDoor(1f);
            this.gemHolder.SetActive(true);
            this.gem.SetPositionAndRotation(this.gemLanding.position, this.gemLanding.rotation);
        }

        // ── Player lock ─────────────────────────────────────────────────────

        // The door-transition input map only has the skip action: gameplay input is ignored.
        private void Lock()
        {
            var scope = LifetimeScope.Find<NavigationScope>();
            this.input = scope != null ? scope.Container.Resolve<IInputService>() : null;
            if (this.input == null) return;

            this.input.SwitchToDoorTransition();
            this.locked = true;
        }

        private void Unlock()
        {
            if (!this.locked) return;
            this.locked = false;
            this.input?.SwitchToGameplay();
        }

        // ── Door ────────────────────────────────────────────────────────────

        private IEnumerator OpenDoor()
        {
            this.door.localEulerAngles = this.doorStartLocalEuler;
            this.doorOpenEvent.Post(this.door.gameObject);

            Coroutine? gemFall = null;
            for (float t = 0f; t < 1f; t += Time.deltaTime / this.doorDuration)
            {
                // Ease-out: the door starts fast and settles as it reaches the open position.
                SetDoor(1f - (1f - t) * (1f - t));

                // The gem slips out as soon as the door gives way, while it is still swinging.
                if (gemFall == null && t >= this.gemDropAtDoor)
                    gemFall = StartCoroutine(DropGem());
                yield return null;
            }
            SetDoor(1f);

            gemFall ??= StartCoroutine(DropGem());
            yield return gemFall; // the rat waits until the gem has landed
        }

        // k: 0 = closed (authored rotation), 1 = open (local Z = doorOpenLocalZ).
        private void SetDoor(float k)
        {
            float z = Mathf.LerpAngle(this.doorStartLocalEuler.z, this.doorOpenLocalZ, k);
            this.door.localEulerAngles = new Vector3(this.doorStartLocalEuler.x, this.doorStartLocalEuler.y, z);
        }

        // ── Rat ─────────────────────────────────────────────────────────────

        private IEnumerator RatJump()
        {
            this.player = FindFirstObjectByType<PlayerController>();
            var ratTransform = this.rat.transform;

            ratTransform.SetPositionAndRotation(this.ratStart.position, FacingRotation(HeadPoint() - this.ratStart.position));
            this.rat.SetActive(true);
            if (this.peekDuration > 0f)
                yield return new WaitForSeconds(this.peekDuration);

            this.ratAnimator.SetTrigger(AttackTrigger);
            this.stingerEvent.Post(gameObject);

            var from = ratTransform.position;
            for (float t = 0f; t < 1f; t += Time.deltaTime / this.leapDuration)
            {
                // The target moves with the player's head, so the rat always lands on the face.
                var to = FaceTarget(from);
                var pos = Vector3.Lerp(from, to, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * this.leapArcHeight);
                ratTransform.rotation = FacingRotation(pos - ratTransform.position);
                ratTransform.position = pos;
                yield return null;
            }

            this.onImpact.Invoke();
            StartlePlayer();

            // Stays on the face, biting: the Attack animation replays, the rat shakes and the player
            // flinches with every bite. All animation, no damage.
            var nextBite = this.biteInterval; // the first bite is the impact itself
            for (float t = 0f; t < this.biteDuration; t += Time.deltaTime)
            {
                if (t >= nextBite)
                {
                    nextBite += this.biteInterval;
                    this.ratAnimator.SetTrigger(AttackTrigger);
                    if (this.player != null) this.player.Flinch();
                }

                var position = FaceTarget(ratTransform.position) + Random.insideUnitSphere * this.biteShake;
                var jitter = Quaternion.Euler(Random.Range(-this.biteJitter, this.biteJitter), Random.Range(-this.biteJitter, this.biteJitter), Random.Range(-this.biteJitter, this.biteJitter));
                ratTransform.SetPositionAndRotation(position, FacingRotation(HeadPoint() - position) * jitter);
                yield return null;
            }
        }

        // The player flinches and steps back, away from where the rat came from.
        private void StartlePlayer()
        {
            if (this.player == null) return;
            this.player.Stagger(this.player.transform.position - this.ratStart.position, this.staggerDistance, this.staggerDuration);
        }

        private IEnumerator RatEscape()
        {
            var ratTransform = this.rat.transform;
            var head = ratTransform.position;
            var floorY = FloorHeightBelow(head);

            // Lets go and hops off the face, landing a little way along the escape route.
            var landing = new Vector3(head.x, floorY, head.z);
            var exit = ChooseEscapeDirection(landing);
            landing += exit * this.hopAwayDistance;

            var start = ratTransform.position;
            var startRotation = ratTransform.rotation;
            var endRotation = FacingRotation(exit);
            var duration = Mathf.Max(this.dropDuration, Mathf.Sqrt(2f * Mathf.Max(0.01f, start.y - floorY) / FallGravity));
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                var horizontal = Vector3.Lerp(start, landing, t);
                horizontal.y = Mathf.Lerp(start.y, landing.y, t * t); // ease-in: gravity
                ratTransform.SetPositionAndRotation(horizontal, Quaternion.Slerp(startRotation, endRotation, t));
                yield return null;
            }
            ratTransform.SetPositionAndRotation(landing, endRotation);

            if (this.landPause > 0f)
                yield return new WaitForSeconds(this.landPause);

            this.ratAnimator.SetBool(RunBool, true);
            var camera = Camera.main;
            while (camera != null && IsInView(camera, ratTransform.position))
            {
                ratTransform.position += exit * (this.runSpeed * Time.deltaTime);
                yield return null;
            }

            // Out of sight: a last short stretch so it's gone for sure before it's hidden.
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                ratTransform.position += exit * (this.runSpeed * Time.deltaTime);
                yield return null;
            }
            this.ratAnimator.SetBool(RunBool, false);
        }

        // Front of the player's head, towards where the rat comes from.
        private Vector3 FaceTarget(Vector3 ratPosition)
        {
            var head = HeadPoint();
            var toRat = ratPosition - head;
            toRat.y = 0f;
            return toRat.sqrMagnitude < 1e-4f ? head : head + toRat.normalized * 0.12f;
        }

        private Vector3 HeadPoint()
        {
            if (this.player == null) return this.ratStart.position + this.ratStart.forward;

            var animator = this.player.GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                if (head != null) return head.position;
            }
            return this.player.transform.position + Vector3.up * 0.6f;
        }

        // The model looks along -Z.
        private static Quaternion FacingRotation(Vector3 direction)
        {
            return direction.sqrMagnitude < 1e-6f ? Quaternion.identity : Quaternion.LookRotation(-direction.normalized);
        }

        // ── Escape route ────────────────────────────────────────────────────

        // Horizontal direction that takes the rat out of the camera's view soonest without running
        // into a wall. An assigned escapePoint wins; with no camera, away from the locker.
        private Vector3 ChooseEscapeDirection(Vector3 from)
        {
            if (this.escapePoint != null)
            {
                var direct = this.escapePoint.position - from;
                direct.y = 0f;
                if (direct.sqrMagnitude > 1e-4f) return direct.normalized;
            }

            var camera = Camera.main;
            var best = Vector3.zero;
            var bestExit = float.MaxValue;
            var fallback = Vector3.zero;
            var fallbackFree = -1f;

            const int Directions = 24;
            const float Step = 0.25f;
            const float MaxDistance = 12f;
            for (var i = 0; i < Directions; i++)
            {
                var direction = Quaternion.AngleAxis(i * 360f / Directions, Vector3.up) * Vector3.forward;
                var free = FreeDistance(from, direction, MaxDistance);
                if (free > fallbackFree)
                {
                    fallbackFree = free;
                    fallback = direction;
                }

                if (camera == null) continue;
                for (var d = Step; d <= free; d += Step)
                {
                    if (IsInView(camera, from + direction * d)) continue;
                    if (d < bestExit)
                    {
                        bestExit = d;
                        best = direction;
                    }
                    break;
                }
            }

            return best != Vector3.zero ? best : fallback;
        }

        private float FreeDistance(Vector3 from, Vector3 direction, float max)
        {
            var origin = from + Vector3.up * 0.1f;
            var best = max;
            foreach (var hit in Physics.RaycastAll(origin, direction, max, ~0, QueryTriggerInteraction.Ignore))
            {
                if (IgnoredCollider(hit.collider)) continue;
                best = Mathf.Min(best, hit.distance);
            }
            return best;
        }

        private static bool IsInView(Camera camera, Vector3 point)
        {
            var v = camera.WorldToViewportPoint(point);
            return v.z > 0f && v.x > -0.05f && v.x < 1.05f && v.y > -0.05f && v.y < 1.05f;
        }

        private bool IgnoredCollider(Collider collider)
        {
            var t = collider.transform;
            return t.IsChildOf(this.rat.transform)
                || (this.player != null && t.IsChildOf(this.player.transform))
                || t.IsChildOf(this.gemHolder.transform);
        }

        private float FloorHeightBelow(Vector3 from)
        {
            var best = float.NegativeInfinity;
            foreach (var hit in Physics.RaycastAll(from, Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (IgnoredCollider(hit.collider) || hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.point.y > best) best = hit.point.y;
            }
            return float.IsNegativeInfinity(best) ? from.y : best;
        }

        // ── Gem ─────────────────────────────────────────────────────────────

        // The gem slips out of the locker as the door opens: a small hop outwards, then it drops to
        // gemLanding with gravity, tumbling a little on the way.
        private IEnumerator DropGem()
        {
            this.gemHolder.SetActive(true);

            var startRoot = this.gem.position;
            var startRotation = this.gem.rotation;
            var endRoot = this.gemLanding.position;
            var endRotation = this.gemLanding.rotation;

            // The pickup's pivot sits well below its mesh, so it is moved by the mesh's centre:
            // it tumbles in place and the mesh reaches the floor exactly when t hits 1.
            var meshCentre = this.gem.GetComponentInChildren<Renderer>().bounds.center;
            var centreOffset = Quaternion.Inverse(startRotation) * (meshCentre - startRoot); // in the gem's own frame
            var startCentre = startRoot + startRotation * centreOffset;
            var endCentre = endRoot + endRotation * centreOffset;

            // Posted a little before the impact to make up for the sound engine's latency.
            var soundAt = Mathf.Clamp01(1f - this.gemDropSoundLead / this.gemFlightDuration);
            var posted = false;

            for (float t = 0f; t < 1f; t += Time.deltaTime / this.gemFlightDuration)
            {
                var centre = Vector3.Lerp(startCentre, endCentre, t);
                centre.y = Mathf.Lerp(startCentre.y, endCentre.y, t * t) + Mathf.Sin(t * Mathf.PI) * this.gemArcHeight; // ease-in: gravity
                var rotation = Quaternion.Slerp(startRotation, endRotation, t) * Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI) * 120f);
                this.gem.SetPositionAndRotation(centre - rotation * centreOffset, rotation);

                if (!posted && t >= soundAt)
                {
                    posted = true;
                    this.gemDropEvent.Post(this.gem.gameObject);
                }
                yield return null;
            }

            this.gem.SetPositionAndRotation(endRoot, endRotation);
            if (!posted) this.gemDropEvent.Post(this.gem.gameObject);
        }
    }
}
