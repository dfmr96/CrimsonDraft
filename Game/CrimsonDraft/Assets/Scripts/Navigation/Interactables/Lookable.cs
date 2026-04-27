#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class Lookable : MonoBehaviour
    {
        [SerializeField] private Vector3 offset;
        [SerializeField] private int priority;

        public int Priority => priority;
        public Vector3 LookPosition => transform.TransformPoint(offset);
    }
}
