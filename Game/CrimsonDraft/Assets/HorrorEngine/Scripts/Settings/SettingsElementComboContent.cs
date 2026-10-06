using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    public abstract class SettingsElementComboContent : SettingsElementContent
    {
        public abstract string GetItemName(int index);
        public abstract int GetItemCount();

        public int GetItemIndex(string name)
        {
            int count = GetItemCount();
            for (int i = 0; i < count; ++i)
            {
                if (name == GetItemName(i))
                    return i;
            }

            Debug.LogWarning("Settings combo item index could not be found. Make sure the default value is part of the combo values : " + name);
            return -1;
        }
    }
}

