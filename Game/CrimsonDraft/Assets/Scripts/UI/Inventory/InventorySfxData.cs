#nullable enable

using UnityEngine;

namespace CrimsonDraft.UI
{
    [CreateAssetMenu(menuName = "CrimsonDraft/UI/Inventory Sfx Data")]
    public sealed class InventorySfxData : ScriptableObject
    {
        [SerializeField] private AK.Wwise.Event cursorEvent = new();
        [SerializeField] private AK.Wwise.Event decideEvent = new();
        [SerializeField] private AK.Wwise.Event cancelEvent = new();

        public void PlayCursor(GameObject postFrom) => this.cursorEvent.Post(postFrom);
        public void PlayDecide(GameObject postFrom) => this.decideEvent.Post(postFrom);
        public void PlayCancel(GameObject postFrom) => this.cancelEvent.Post(postFrom);
    }
}
