using UnityEngine;

namespace HorrorEngine
{
    public class SequencePuzzleText : SequencePuzzleTextBase
    {
        [SerializeField] TMPro.TextMeshPro m_Text;

        protected override string Text { get => m_Text.text; set => m_Text.text = value; }
    }
}