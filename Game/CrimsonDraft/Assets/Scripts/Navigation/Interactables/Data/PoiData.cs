#nullable enable

using UnityEngine;
using Yarn.Unity;

namespace CrimsonDraft.Navigation.Interactables
{
    [CreateAssetMenu(menuName = "CrimsonDraft/Interactables/PoiData")]
    public sealed class PoiData : ScriptableObject
    {
        [SerializeField] private DialogueReference dialogueReference = new();

        public DialogueReference DialogueReference => this.dialogueReference;
    }
}
