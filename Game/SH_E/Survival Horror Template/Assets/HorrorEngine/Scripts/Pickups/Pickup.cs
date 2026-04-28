using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    public abstract class Pickup : MonoBehaviour
    {
        public UnityEvent OnPickup;
        public AudioClip PickupSound;

        public virtual void Take() 
        { 
            OnPickup?.Invoke();

            if (PickupSound)
            {
                AudioSource playerSource = GameManager.Instance.Player.GetComponent<AudioSource>();
                if (playerSource)
                    playerSource.PlayOneShot(PickupSound);
            }
        }
    }
}