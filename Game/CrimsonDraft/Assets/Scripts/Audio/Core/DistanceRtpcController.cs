#nullable enable

using UnityEngine;

namespace CrimsonDraft.Audio
{
    // Place on the GameObject that emits the sound driven by this RTPC.
    public sealed class DistanceRtpcController : MonoBehaviour
    {
        [Header("Wwise")]
        [SerializeField] private AK.Wwise.RTPC rtpc = new();
         [SerializeField] private AK.Wwise.RTPC seartpc = new();

        [Header("Distance")]
        [SerializeField] private Transform player = null!;
        [SerializeField] private Transform target = null!;
        [SerializeField] private float distanceFactor;
        [SerializeField] private float maxDistance;

 private bool disabledDueToError;

        private void Awake()
        {
            
            if (player == null || target == null)
            {
                Debug.LogError($"[DistanceRtpcController] Missing Player or Target reference on '{gameObject.name}' — disabling.", this);
                enabled = false;
                return;
            }

            if (maxDistance <= 0f)
                Debug.LogWarning($"[DistanceRtpcController] maxDistance is 0 on '{gameObject.name}' — proximity value will always be 0.", this);
        }
        private void Update()
        {
            if (disabledDueToError) return;
            var distance = Vector3.Distance(player.position, target.position);
            var proximity = maxDistance > 0f
                ? (1f - Mathf.Clamp01(distance / maxDistance)) * 100f
                : 0f;

            try
            {
            rtpc.SetValue(gameObject, distance * distanceFactor);
            seartpc.SetValue(gameObject, proximity * distanceFactor);
            }
            catch (System.DllNotFoundException)
            {
                disabledDueToError = true;
                Debug.LogError("[DistanceRtpcController] AkSoundEngine native plugin not found — disabling RTPC updates for this session.", this);
            }//Debug.Log($"[DistanceRtpcController] {rtpc.Name} (Wwise) = {rtpc.GetValue(gameObject)}");
            
        }
    }
}
