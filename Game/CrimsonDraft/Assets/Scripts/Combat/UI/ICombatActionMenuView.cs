#nullable enable

using System;

namespace CrimsonDraft.Combat
{
    public interface ICombatActionMenuView
    {
        event Action? OnDisparar;
        event Action? OnCerrar;
    }
}
