#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    public sealed class QTEView : MonoBehaviour, IQTEView
    {
        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);
    }
}
