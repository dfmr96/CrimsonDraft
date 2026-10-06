using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine 
{
    public class SequencePuzzleTextUI : SequencePuzzleTextBase
    {
        [SerializeField] TMPro.TextMeshProUGUI m_Text;

        protected override string Text { get => m_Text.text; set => m_Text.text = value; }
    }
}