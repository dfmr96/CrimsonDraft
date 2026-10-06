using UnityEngine;

namespace HorrorEngine
{
    public class PlayerInteractionProxy : MonoBehaviour
    {
        public void Interact(Interactive Interactive)
        {
            var interactor = GameManager.Instance.Player.GetComponent<PlayerInteractor>();
            Interactive.Interact(interactor);
        }
    }
}