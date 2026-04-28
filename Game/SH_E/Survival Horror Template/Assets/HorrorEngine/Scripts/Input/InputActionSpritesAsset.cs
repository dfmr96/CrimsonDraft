using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace HorrorEngine
{

    [Serializable]
    public class InputActionSprite
    {
        [FormerlySerializedAs("ActionPath")]
        public string ControlPath;
        public Sprite Sprite;
    }

    [Serializable]
    public class InputActionSpritesScheme
    {
        public InputSchemeHandle ControlScheme;
        public InputActionSprite[] Entries;

        private Dictionary<string, InputActionSprite> m_HashedEntries;

        public Sprite GetSprite(string actionPath)
        {
            if (m_HashedEntries == null) 
            {
                m_HashedEntries = new Dictionary<string, InputActionSprite>();
            
                foreach (var entry in Entries)
                {
                    if (!m_HashedEntries.ContainsKey(entry.ControlPath))
                    {
                        m_HashedEntries.Add(entry.ControlPath, entry);
                    }
                    else
                    {
                        Debug.LogError($"Duplicated ActionPath in InputActionSpritesAsset {entry.ControlPath}");
                    }
                }
            }

            if (m_HashedEntries.ContainsKey(actionPath))
            {
                return m_HashedEntries[actionPath].Sprite;
            }
            else
            {
                Debug.LogError($"InputActionSpritesAsset doesn't contain sprite for path {actionPath}");
                return null;
            }
        }
    }

    [CreateAssetMenu(fileName = "InputActionSpritesAsset", menuName = "Horror Engine/Input/InputActionSpritesAsset")]
    public class InputActionSpritesAsset : ScriptableObject
    {
        public Sprite NotFoundSprite;
        public InputActionSpritesScheme[] Schemes;

        private Dictionary<InputSchemeHandle, InputActionSpritesScheme> m_HashedSchemes;

        public InputActionSpritesScheme GetScheme(InputSchemeHandle schemeHandle)
        {
            if (m_HashedSchemes == null)
            {
                m_HashedSchemes = new Dictionary<InputSchemeHandle, InputActionSpritesScheme>();

                foreach (var scheme in Schemes)
                {
                    if (!m_HashedSchemes.ContainsKey(scheme.ControlScheme))
                    {
                        m_HashedSchemes.Add(scheme.ControlScheme, scheme);
                    }
                    else
                    {
                        Debug.LogError($"Duplicated ControlSchemeName in InputActionGlyphAsset {scheme.ControlScheme.SchemeName}", this);
                    }
                }
            }

            if (m_HashedSchemes.ContainsKey(schemeHandle))
            {
                return m_HashedSchemes[schemeHandle];
            }
            else
            {
                Debug.LogError($"InputActionGlyphAsset doesn't contain scheme {schemeHandle.SchemeName}");
                return null;
            }
        }
    }
}