#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    public interface IRandomSource
    {
        float NextFloat01();
        int NextInt(int minInclusive, int maxExclusive);
    }

    public sealed class UnityRandomSource : IRandomSource
    {
        public float NextFloat01() => Random.value;

        public int NextInt(int minInclusive, int maxExclusive) => Random.Range(minInclusive, maxExclusive);
    }
}
