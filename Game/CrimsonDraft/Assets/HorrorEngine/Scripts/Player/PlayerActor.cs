using UnityEngine;
using UnityEngine.AI;

namespace HorrorEngine
{
    public class PlayerActor : Actor
    {
        [HideInInspector]
        public CharacterData Character;

        // --------------------------------------------------------------------

        public void SetVisible(bool visible)
        {
            MainAnimator.gameObject.SetActive(visible);
        }

        
    }

}