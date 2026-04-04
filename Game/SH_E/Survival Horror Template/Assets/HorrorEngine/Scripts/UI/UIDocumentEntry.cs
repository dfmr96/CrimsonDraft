using UnityEngine;
using UnityEngine.UI;

namespace HorrorEngine
{
    public class UIDocumentEntry : MonoBehaviour
    {
        [SerializeField] private Image m_Icon;
        [SerializeField] private Sprite m_EmptySprite;

        public DocumentData Data { get; private set; }

        public void Fill(DocumentData document)
        {
            Data = document;
            m_Icon.sprite = Data ? Data.Image : m_EmptySprite;
        }
    }
}