using UnityEngine;

namespace HorrorEngine
{
    public class Register : ScriptableObject
    {
        public string UniqueId;

        private void OnValidate()
        {
            GenerateId();
        }

        public void GenerateId()
        {
            if (string.IsNullOrEmpty(UniqueId))
                UniqueId = IdUtils.GenerateId();
        }
    }
}