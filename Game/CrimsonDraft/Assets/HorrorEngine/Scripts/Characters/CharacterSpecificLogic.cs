using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorEngine
{
    [Serializable]
    public class CharacterLogicEntry

    {
        public CharacterData Character;
        public UnityEvent OnCharacterEvent;
    }

    public class CharacterSpecificLogic
        : MonoBehaviour
    {
        [SerializeField] List<CharacterLogicEntry> Entries;
        [SerializeField] bool TriggerOnEnable;

        private void OnEnable()
        {
            if (TriggerOnEnable)
            {
                Trigger();
            }
        }

        public void Trigger()
        {
            var character = GameManager.Instance.Character;
            foreach (var entry in Entries)
            {
                if (entry.Character == character)
                {
                    entry.OnCharacterEvent?.Invoke();
                }
            }
        }
    }
}
