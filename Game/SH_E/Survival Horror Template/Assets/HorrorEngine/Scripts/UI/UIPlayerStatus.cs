namespace HorrorEngine
{
    public class UIPlayerStatus : UIStatus
    {
        private void Start()
        {
            if (GameManager.Exists)
            {
                if (GameManager.Instance.Player)
                {
                    BindToActor(GameManager.Instance.Player);
                }
                else
                {
                    GameManager.Instance.OnPlayerRegistered.AddListener(OnPlayerRegistered);
                }
            }
        }

        // --------------------------------------------------------------------

        private void OnPlayerRegistered(PlayerActor player)
        {
            BindToActor(player);

            GameManager.Instance.OnPlayerRegistered.RemoveListener(OnPlayerRegistered);
        }

    }
}
