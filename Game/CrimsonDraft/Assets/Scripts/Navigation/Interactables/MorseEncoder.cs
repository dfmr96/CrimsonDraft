#nullable enable

using System.Collections.Generic;

namespace CrimsonDraft.Navigation.Interactables
{
    /// <summary>One element of a Morse transmission: a tone (key down) or a silence, in Morse units.</summary>
    public readonly struct MorseStep
    {
        public MorseStep(bool tone, int units)
        {
            this.Tone  = tone;
            this.Units = units;
        }

        public bool Tone  { get; }
        public int  Units { get; }
    }

    /// <summary>
    /// Text to Morse timing: dot = 1 unit, dash = 3, gap between the dots/dashes of a letter = 1,
    /// between letters = 3, between words = 7. Letters come from <see cref="MorseDecoder"/>'s
    /// table (the same one the BEEPER tab decodes), digits are added here.
    /// </summary>
    public static class MorseEncoder
    {
        private static readonly string[] s_digits =
        {
            "-----", ".----", "..---", "...--", "....-", ".....", "-....", "--...", "---..", "----.",
        };

        public static bool TryGetCode(char c, out string code)
        {
            c = char.ToUpperInvariant(c);
            if (c >= '0' && c <= '9')
            {
                code = s_digits[c - '0'];
                return true;
            }
            return MorseDecoder.TryGetCode(c, out code);
        }

        /// <summary>Characters without a Morse code are skipped.</summary>
        public static List<MorseStep> Encode(string text)
        {
            var steps = new List<MorseStep>();
            bool pendingWordGap = false;

            foreach (char c in text)
            {
                if (c == ' ')
                {
                    pendingWordGap = steps.Count > 0;
                    continue;
                }
                if (!TryGetCode(c, out string code)) continue;

                if (steps.Count > 0)
                    steps.Add(new MorseStep(false, pendingWordGap ? 7 : 3));
                pendingWordGap = false;

                for (int i = 0; i < code.Length; i++)
                {
                    steps.Add(new MorseStep(true, code[i] == '-' ? 3 : 1));
                    if (i < code.Length - 1) steps.Add(new MorseStep(false, 1));
                }
            }

            return steps;
        }

        public static int TotalUnits(IReadOnlyList<MorseStep> steps)
        {
            int total = 0;
            foreach (var step in steps) total += step.Units;
            return total;
        }
    }
}
