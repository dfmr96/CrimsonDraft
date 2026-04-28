using System;
using UnityEngine;
using UnityEngine.Localization;

namespace HorrorEngine
{
    [Serializable]
    public class LocalizableSprite
    {
        public bool IsLocalized;
        public Sprite Unlocalized;
        public LocalizedSprite Localized;

        public static implicit operator Sprite(LocalizableSprite localSprite) 
        {
            if (localSprite == null)
                return null;

            return localSprite.Localized == null || localSprite.Localized.IsEmpty ? localSprite.Unlocalized : localSprite.Localized.LoadAsset();
        }

        public static implicit operator bool(LocalizableSprite localSprite)
        {
            return (localSprite.Localized != null && !localSprite.Localized.IsEmpty) || localSprite.Unlocalized;
        }

    }
}