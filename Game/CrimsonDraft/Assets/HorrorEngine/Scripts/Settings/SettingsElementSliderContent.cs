using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    public abstract class SettingsElementSliderContent : SettingsElementContent
    {
        public float MinSliderValue = 0f;
        public float MaxSliderValue = 1f;
        public bool WholeNumbers = false;
        public string TextFormat = "{0}";
    }
}
