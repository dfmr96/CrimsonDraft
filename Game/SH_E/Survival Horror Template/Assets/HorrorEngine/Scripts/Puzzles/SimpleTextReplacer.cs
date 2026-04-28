using UnityEngine;

namespace HorrorEngine
{
    public class SimpleTextReplacer : TextReplacerBase
    {
        [SerializeField] string m_ReplaceText;

        public override string Replace(string text)
        {
            return m_ReplaceText;
        }
    }
}
