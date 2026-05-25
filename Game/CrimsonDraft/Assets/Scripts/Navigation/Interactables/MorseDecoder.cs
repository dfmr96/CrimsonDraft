#nullable enable

using System.Collections.Generic;
using System.Text;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class MorseDecoder
    {
        private readonly StringBuilder _currentSequence = new();
        private readonly List<char>    _word            = new();

        public string              CurrentSequence => _currentSequence.ToString();
        public IReadOnlyList<char> Word            => _word;

        public void InputDot()  => _currentSequence.Append('.');
        public void InputDash() => _currentSequence.Append('-');

        public void Confirm()   { }
        public void Backspace() { }
        public void Reset()     { }
        public string GetWord() => "";
    }
}
