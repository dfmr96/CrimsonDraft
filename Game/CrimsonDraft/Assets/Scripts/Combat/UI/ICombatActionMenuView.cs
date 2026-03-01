#nullable enable

using System;

namespace CrimsonDraft.Combat
{
    public interface ICombatActionMenuView
    {
        event Action<int>? OnOperatorSelected;
    }
}
