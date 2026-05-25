#nullable enable

using System.Collections.Generic;
using System.Text;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class MorseDecoder
    {
        private static readonly Dictionary<string, char> s_table = new()
        {
            ["-"]    = 'T', ["."]    = 'E',
            ["--"]   = 'M', ["-."]   = 'N', [".-"]   = 'A', [".."]   = 'I',
            ["---"]  = 'O', ["--."]  = 'G', ["-.-"]  = 'K', ["-.."]  = 'D',
            [".--"]  = 'W', [".-."]  = 'R', ["..-"]  = 'U', ["..."]  = 'S',
            ["--.-"] = 'Q', ["--.."] = 'Z', ["-.--"] = 'Y', ["-.-."] = 'C',
            ["-..-"] = 'X', ["-..."] = 'B', [".---"] = 'J', [".--."]=  'P',
            [".-.."] = 'L', ["..-."] = 'F', ["...-"] = 'V', ["...."] = 'H',
        };

        private readonly StringBuilder _currentSequence = new();
        private readonly List<char>    _word            = new();

        public string              CurrentSequence => _currentSequence.ToString();
        public IReadOnlyList<char> Word            => _word;

        public void InputDot()  => _currentSequence.Append('.');
        public void InputDash() => _currentSequence.Append('-');

        public void Confirm()
        {
            var seq = _currentSequence.ToString();
            if (seq.Length == 0) return;
            if (!s_table.TryGetValue(seq, out var letter)) return;
            _word.Add(letter);
            _currentSequence.Clear();
        }

        public void Backspace() { }
        public void Reset()     { }
        public string GetWord() => "";
    }
}
