using UnityEngine;

namespace HorrorEngine
{
    public enum GameAttributeDomain
    {
        Character,
        Global
    }

    [CreateAssetMenu(menuName = "Horror Engine/Attributes/GameAttribute")]
    public class GameAttribute : Register
    {
        public GameAttributeDomain Domain;
    }
}