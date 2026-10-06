using System;
using UnityEngine;

namespace HorrorEngine
{
    public interface ITextReplacer 
    {
        string Replace(string text);
    }

    public class TextReplacerBase : MonoBehaviour, ITextReplacer
    {
        public virtual string Replace(string text)
        {
            return text;
        }
    }
}