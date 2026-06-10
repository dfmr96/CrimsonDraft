#nullable enable

using System;
using NaughtyAttributes;
using UnityEngine;
using VContainer;
using CrimsonDraft.Infrastructure;

namespace CrimsonDraft.Navigation
{
    public sealed class PickupRegistryDebugView : MonoBehaviour
    {
        [ReadOnly] [SerializeField] private int      collectedCount;
        [ReadOnly] [SerializeField] private string[] collectedIds = Array.Empty<string>();

        private PickupRegistry? registry;

        [Inject]
        public void Construct(PickupRegistry registry)
        {
            this.registry = registry;
        }

#if UNITY_EDITOR
        private void Update()
        {
            if (this.registry == null) return;
            this.collectedCount = this.registry.CollectedIds.Count;

            var ids = this.registry.CollectedIds;
            if (this.collectedIds.Length != ids.Count)
                Array.Resize(ref this.collectedIds, ids.Count);

            var i = 0;
            foreach (var id in ids)
                this.collectedIds[i++] = id;
        }
#endif
    }
}
