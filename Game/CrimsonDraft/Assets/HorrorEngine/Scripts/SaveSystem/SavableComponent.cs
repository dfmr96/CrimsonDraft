using UnityEngine;

namespace HorrorEngine
{
    public abstract class SavableComponent : MonoBehaviour, ISavable<string>
    {
        public virtual string GetSavableData()
        {
            throw new System.NotImplementedException();
        }

        public virtual void SetFromSavedData(string savedData)
        {
            throw new System.NotImplementedException();
        }

    }
}