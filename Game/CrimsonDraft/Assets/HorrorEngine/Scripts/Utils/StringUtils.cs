using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;

namespace HorrorEngine
{
    public static class StringUtils
    {
        public static void ReplaceTagInArray(ref string[] lines, string tag, string newText)
        {
            for (int i = 0; i < lines.Length; ++i)
            {
                lines[i] = lines[i].Replace(tag, newText);
            }
        }

        // --------------------------------------------------------------------

        public static string SplitCamelCase(this string str)
        {
            return Regex.Replace(
                Regex.Replace(
                    str,
                    @"(\P{Ll})(\P{Ll}\p{Ll})",
                    "$1 $2"
                ),
                @"(\p{Ll})(\P{Ll})",
                "$1 $2"
            );
        }
    }
}