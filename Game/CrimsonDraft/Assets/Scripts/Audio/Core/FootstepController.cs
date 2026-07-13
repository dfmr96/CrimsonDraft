#nullable enable

using UnityEngine;

namespace CrimsonDraft.Audio
{
    // The Wwise-side footstep sound loops on its own, so this only needs to
    // Post once when movement starts (or switches between walk/run) and Stop
    // when the player stops moving — no per-step retriggering needed.
    public sealed class FootstepController : MonoBehaviour
    {
        [Header("Wwise")]
        [SerializeField] private AK.Wwise.Event  footstepEvent = new();
        [SerializeField] private AK.Wwise.Switch walkSwitch    = new();
        [SerializeField] private AK.Wwise.Switch runSwitch     = new();

        [Header("Player")]
        [Tooltip("A GameObject with a component implementing IPlayerMotion (e.g. PlayerController).")]
        [SerializeField] private GameObject playerObject = null!;

        private IPlayerMotion player = null!;
        private bool          wasMoving;
        private bool          wasSprinting;

        private void Awake()
        {
            player = playerObject.GetComponent<IPlayerMotion>();
            if (player == null)
                Debug.LogError($"[FootstepController] '{playerObject.name}' has no component implementing IPlayerMotion.", this);
        }

        private void Update()
        {
            if (player == null) return;

            var moving    = player.CurrentSpeed > 0f;
            var sprinting = player.IsSprinting;

            if (moving && (!wasMoving || sprinting != wasSprinting))
            {
                if (wasMoving)
                    footstepEvent.Stop(gameObject);

                var speedSwitch = sprinting ? runSwitch : walkSwitch;
                speedSwitch.SetValue(gameObject);
                footstepEvent.Post(gameObject);
            }
            else if (!moving && wasMoving)
            {
                footstepEvent.Stop(gameObject);
            }

            wasMoving    = moving;
            wasSprinting = sprinting;
        }
    }
}
