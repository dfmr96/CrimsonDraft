using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace HorrorEngine
{
    [CreateAssetMenu(fileName = "DefaultSettings", menuName = "Horror Engine/Settings/DefaultSettings", order = -5)]
    public class GameSettingsDefaults : ScriptableObject
    {
        public PlayerMovementType MovementType;
    }
}
