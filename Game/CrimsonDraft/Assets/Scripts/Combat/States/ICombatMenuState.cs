#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    public interface ICombatMenuState
    {
        void Enter()                              { }
        void Exit()                               { }
        void OnCancel()                           { }
        void OnConfirm()                          { }
        void OnNavigate(Vector2 dir)              { }
        void OnOperatorSelected(int index)        { }
        void OnOperatorFocused(int index)         { }
        void OnCommandSelected(CombatCommand cmd) { }
        void OnItemSelected(int index)            { }
    }
}
