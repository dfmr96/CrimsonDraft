#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Tests
{
    // In-memory 4x4 sprites for tests that need real Sprite/Texture2D objects. Dispose
    // destroys everything it created.
    internal sealed class TestSprites : System.IDisposable
    {
        private const int Size = 4;
        private readonly List<Object> created = new List<Object>();

        public Sprite Solid(Color color, bool readable = true) => Make((x, y) => color, readable);

        public Sprite LeftHalf(Color left, Color right) => Make((x, y) => x < Size / 2 ? left : right, true);

        private Sprite Make(System.Func<int, int, Color> pixel, bool readable)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    tex.SetPixel(x, y, pixel(x, y));
            tex.Apply(false, makeNoLongerReadable: !readable);

            var sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f));
            this.created.Add(tex);
            this.created.Add(sprite);
            return sprite;
        }

        public void Dispose()
        {
            foreach (var obj in this.created)
                if (obj != null) Object.DestroyImmediate(obj);
            this.created.Clear();
        }
    }
}
