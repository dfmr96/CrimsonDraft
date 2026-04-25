#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    [CreateAssetMenu(menuName = "CrimsonDraft/Interactables/PoiData")]
    public sealed class PoiData : ScriptableObject
    {
        [SerializeField] private string yarnNodeName = "";

        public string YarnNodeName => this.yarnNodeName;
    }
}
