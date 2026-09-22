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

        private static readonly Dictionary<char, char> s_lastSymbol = BuildLastSymbolLookup();

        private static Dictionary<char, char> BuildLastSymbolLookup()
        {
            var map = new Dictionary<char, char>();
            foreach (var kv in s_table)
                map[kv.Value] = kv.Key[^1];
            return map;
        }

        /// <summary>Last dot/dash of a letter's code — which sprite (point/line) its LED uses.</summary>
        public static bool TryGetLastSymbol(char letter, out char symbol) => s_lastSymbol.TryGetValue(letter, out symbol);

        private static readonly Dictionary<char, string> s_reverseTable = BuildReverseLookup();

        private static Dictionary<char, string> BuildReverseLookup()
        {
            var map = new Dictionary<char, string>();
            foreach (var kv in s_table)
                map[kv.Value] = kv.Key;
            return map;
        }

        /// <summary>The dot/dash pattern (e.g. "...") that encodes a decoded letter — used to
        /// play back a sent word as real Morse tones.</summary>
        public static bool TryGetCode(char letter, out string code) => s_reverseTable.TryGetValue(letter, out code!);

        private readonly StringBuilder _currentSequence = new();
        private readonly List<char>    _word            = new();

        public string              CurrentSequence => _currentSequence.ToString();
        public IReadOnlyList<char> Word            => _word;

        public void InputDot()  => _currentSequence.Append('.');
        public void InputDash() => _currentSequence.Append('-');

        /// <summary>What confirming CurrentSequence + symbol would decode to, without mutating state.</summary>
        public bool TryPreview(char symbol, out char letter) => s_table.TryGetValue(_currentSequence.ToString() + symbol, out letter);

        public void Confirm()
        {
            var seq = _currentSequence.ToString();
            _currentSequence.Clear();
            if (seq.Length == 0) return;
            if (!s_table.TryGetValue(seq, out var letter)) return;
            _word.Add(letter);
        }

        public void Backspace()
        {
            if (_currentSequence.Length > 0)
            {
                _currentSequence.Clear();
                return;
            }
            if (_word.Count > 0)
                _word.RemoveAt(_word.Count - 1);
        }
        public void Reset()
        {
            _currentSequence.Clear();
            _word.Clear();
        }
        public string GetWord() => new string(_word.ToArray());
    }
}
