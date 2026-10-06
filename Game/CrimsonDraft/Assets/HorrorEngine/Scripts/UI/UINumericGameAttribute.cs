using UnityEngine;
using UnityEngine.Serialization;

namespace HorrorEngine
{
    public class UINumericGameAttribute : MonoBehaviour
    {
        [SerializeField] private GameAttribute m_Attribute;
        [SerializeField] private TMPro.TextMeshProUGUI m_Text;
        [SerializeField] private LocalizableText m_Format;
        [Tooltip("How fast the number will change to the new value. Set to 0 for instant change")]
        [SerializeField] private float m_InterpolationSpeed;
        
        private float m_CurrentValue;

        // --------------------------------------------------------------------

        private void Awake()
        {
            MessageBuffer<GameAttributeChangedMessage>.Subscribe(OnGameValueChanged);
            UpdateCurrency(true);
        }

        // --------------------------------------------------------------------

        private void OnDestroy()
        {
            MessageBuffer<GameAttributeChangedMessage>.Unsubscribe(OnGameValueChanged);
        }

        // --------------------------------------------------------------------

        private void OnGameValueChanged(GameAttributeChangedMessage msg)
        {
            if (msg.AttributeId == m_Attribute.UniqueId)
            {
                if (gameObject.activeInHierarchy)
                    enabled = true;
                else
                    UpdateCurrency(true);
            }
        }

        // --------------------------------------------------------------------

        private void OnDisable()
        {
            UpdateCurrency(true);
        }

        // --------------------------------------------------------------------

        void Update()
        {
            UpdateCurrency(m_InterpolationSpeed <= 0 ? true : false);
        }

        // --------------------------------------------------------------------

        void UpdateCurrency(bool immediate)
        {
            int currency = GameAttributeUtils.GetAsInt(m_Attribute);
            float val = currency;
            if (!immediate)
            {
                val = Mathf.MoveTowards(m_CurrentValue, val, Time.unscaledDeltaTime * m_InterpolationSpeed);
            }

            m_CurrentValue = val;
            m_Text.text = string.Format(m_Format, (int)val);

            if (val == currency)
                enabled = false;
        }
    }
}