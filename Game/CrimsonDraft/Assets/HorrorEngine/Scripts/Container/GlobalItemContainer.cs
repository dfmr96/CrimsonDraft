using UnityEngine;

namespace HorrorEngine
{
    public class GlobalItemContainer : ItemContainerBase
    {
        protected override ContainerData GetData()
        {
            return GameManager.Instance.StorageBox;
        }
    }
}