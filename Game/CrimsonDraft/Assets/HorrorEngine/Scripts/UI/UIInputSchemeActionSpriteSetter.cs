using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace HorrorEngine
{
    public class UIInputSchemeActionSpriteSetter : UIInputSchemeSpriteSetter
    {
        [FormerlySerializedAs("Actions")]
        [SerializeField] private InputActionAsset m_Actions;
        [FormerlySerializedAs("ActionName")]
        [SerializeField] private string m_ActionName;
        [FormerlySerializedAs("GlyphAsset")]
        [SerializeField] private InputActionSpritesAsset m_SpriteAsset;


        // --------------------------------------------------------------------

        protected override void Awake()
        {
            base.Awake();
            Debug.Assert(m_Actions, "Actions has not been set on UIInputAction", this);
        }

        // --------------------------------------------------------------------

        protected override void UpdateSprite(InputSchemeHandle scheme)
        {
            var schemeGlyps = m_SpriteAsset.GetScheme(scheme);
            if (schemeGlyps == null)
                return;

            InputAction action = m_Actions.FindAction(m_ActionName);
            foreach (InputBinding binding in action.bindings)
            {
                if (binding.groups.Contains(scheme.SchemeName))
                {
                    Sprite sprite = schemeGlyps.GetSprite(binding.path);
                    if (sprite)
                    {
                        SetSpriteEvent?.Invoke(sprite);
                        return;
                    }
                }
            }

            SetSpriteEvent?.Invoke(m_SpriteAsset.NotFoundSprite);
            NotFoundEvent?.Invoke();
        }
    }
}