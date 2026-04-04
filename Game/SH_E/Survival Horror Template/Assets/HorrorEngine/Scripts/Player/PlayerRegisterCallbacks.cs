using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    public class PlayerRegisterCallbacks : MonoBehaviour
    {
        public UnityEvent<PlayerActor> OnPlayerRegistered;

        void Start()
        {
            if (GameManager.Instance.Player)
            {
                OnPlayerRegisteredListener(GameManager.Instance.Player);
            }
            else
            {
                GameManager.Instance.OnPlayerRegistered.AddListener(OnPlayerRegisteredListener);
            }
        }


        void OnPlayerRegisteredListener(PlayerActor player)
        {
            OnPlayerRegistered?.Invoke(player);
        }

        private void OnDestroy()
        {
            GameManager.Instance.OnPlayerRegistered.RemoveListener(OnPlayerRegisteredListener);
        }
    }
}