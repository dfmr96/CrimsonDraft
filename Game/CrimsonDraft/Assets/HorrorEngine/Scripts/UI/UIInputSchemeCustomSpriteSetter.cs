using System;
using UnityEngine;

namespace HorrorEngine
{
    [Serializable]
    public class CustomInputActionSprite
    {
        public InputSchemeHandle Scheme;
        public Sprite Sprite;
    }

    public class UIInputSchemeCustomSpriteSetter : UIInputSchemeSpriteSetter
    {
        [SerializeField] private CustomInputActionSprite[] m_Sprites;

        protected override void UpdateSprite(InputSchemeHandle scheme)
        {
            foreach(var customSprite in m_Sprites)
            {
                if (customSprite.Scheme == scheme)
                {
                    SetSpriteEvent?.Invoke(customSprite.Sprite);
                    return;
                }
            }

            SetSpriteEvent?.Invoke(null);
            NotFoundEvent?.Invoke();
        }
    }
}