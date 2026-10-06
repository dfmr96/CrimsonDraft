using System;

namespace HorrorEngine
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class LocalizableTextAreaAttribute : Attribute
    {
        public int Size { get; }

        public LocalizableTextAreaAttribute(int size)
        {
            Size = size;
        }
    }
}