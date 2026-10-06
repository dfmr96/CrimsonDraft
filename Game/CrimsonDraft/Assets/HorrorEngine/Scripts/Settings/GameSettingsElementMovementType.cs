using System;
using UnityEngine;

namespace HorrorEngine
{
    public enum PlayerMovementType
    {
        Tank,
        Alternate
    }

    [CreateAssetMenu(fileName = "GameSettingMovement", menuName = "Horror Engine/Settings/MovementType")]
    public class GameSettingsElementMovementType : SettingsElementComboContent
    {
        public override void Apply()
        {
            // Do Nothing - Player movement will read the setting value
        }

        public override string GetDefaultValue()
        {
            return SettingsManager.Instance.Defaults.MovementType.ToString();
        }

        public override int GetItemCount()
        {
            return Enum.GetNames(typeof(PlayerMovementType)).Length;
        }

        public override string GetItemName(int index)
        {
            return Enum.GetNames(typeof(PlayerMovementType))[index];
        }
    }
}