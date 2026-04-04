using System;
using UnityEngine;
using UnityEngine.Localization;

namespace HorrorEngine
{
    [Serializable]
    public class LocalizableText
    {
        public bool IsLocalized;
        public string Unlocalized;
        public LocalizedString Localized;

        /*
         * public static implicit operator OptionalLocalizedString(string input)
        {
            return new OptionalLocalizedString()
            {
                Unlocalized = input
            };
        }
        */

        public static implicit operator string(LocalizableText localStr) 
        {
            if (localStr == null)
                return "";

            return (!localStr.IsLocalized || localStr.Localized == null || localStr.Localized.IsEmpty) ? localStr.Unlocalized : localStr.Localized.GetLocalizedString();
        }

    }
}