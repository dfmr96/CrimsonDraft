#nullable enable

using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Yarn.Unity;

namespace CrimsonDraft.Navigation.Interactables.UI
{
    public static class DocumentMarkupFormatter
    {
        public static string Format(string text, MarkupPalette? palette)
        {
            if (string.IsNullOrEmpty(text) || palette == null)
                return text;

            var builder = new StringBuilder(text.Length);
            var stack = new Stack<MarkupPalette.CustomMarker>();

            for (int index = 0; index < text.Length;)
            {
                if (text[index] != '[')
                {
                    builder.Append(text[index]);
                    index++;
                    continue;
                }

                int markerEnd = text.IndexOf(']', index);
                if (markerEnd < 0)
                {
                    builder.Append(text[index]);
                    index++;
                    continue;
                }

                string token = text.Substring(index + 1, markerEnd - index - 1);
                bool isClosing = token.StartsWith("/");
                string markerName = isClosing ? token.Substring(1) : token;

                if (string.IsNullOrWhiteSpace(markerName) || !palette.PaletteForMarker(markerName, out var marker))
                {
                    builder.Append(text, index, markerEnd - index + 1);
                    index = markerEnd + 1;
                    continue;
                }

                if (!isClosing)
                {
                    stack.Push(marker);
                    builder.Append(marker.Start);
                }
                else if (stack.Count > 0 && stack.Peek().Marker == markerName)
                {
                    builder.Append(stack.Pop().End);
                }
                else
                {
                    builder.Append(text, index, markerEnd - index + 1);
                }

                index = markerEnd + 1;
            }

            while (stack.Count > 0)
                builder.Append(stack.Pop().End);

            return builder.ToString();
        }
    }
}
