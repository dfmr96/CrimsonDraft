using UnityEngine;

namespace HorrorEngine
{
    [RequireComponent(typeof(MapRoomCompletionStep))]
    public class CompleteMapRoomStepOnPickup : MonoBehaviour
    {
        private void Awake()
        {
            Pickup pickup = GetComponent<Pickup>();
            pickup.OnPickup.AddListener(OnCompleted);
        }

        private void OnCompleted()
        {
            GetComponent<MapRoomCompletionStep>().SetCompleted(true);
        }
    }
}