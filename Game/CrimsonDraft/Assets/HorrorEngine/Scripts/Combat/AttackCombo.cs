using System;
using System.Collections;
using UnityEngine;

namespace HorrorEngine
{
    [Serializable]
    public class AttackComboEntry
    {
        public AttackMontage Montage;
        [Tooltip("Indicates at what point in time in the previous attack this entry can take place")]
        public float MinEntryTime;
        [Tooltip("Indicates the extra time after the previous attack finished in which the player can still perform this combo entry")]
        public float GracePeriod;
    }

    public class AttackCombo : MonoBehaviour
    {
        public AttackComboEntry[] Combo;
    }
}