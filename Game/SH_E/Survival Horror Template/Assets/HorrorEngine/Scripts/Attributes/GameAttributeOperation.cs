using UnityEngine;

namespace HorrorEngine
{
    public class GameAttributeOperation : MonoBehaviour
    {
        [SerializeField] private GameAttribute m_Attribute;
        [SerializeField] private GameAttributeOp m_Operation;
        [SerializeField] private float m_Amount;

        public void Perform()
        {
            GameAttributeUtils.PerformOperation(m_Attribute, m_Amount, m_Operation);
        }
    }
}