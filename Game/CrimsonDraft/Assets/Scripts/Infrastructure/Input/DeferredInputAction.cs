#nullable enable

using System;
using UnityEngine.InputSystem;

namespace CrimsonDraft.Infrastructure.Input
{
    /// <summary>
    /// Defers an action to InputSystem.onAfterUpdate so it doesn't run synchronously inside an
    /// InputAction's own 'performed' callback. Enabling/disabling an action map mid-dispatch
    /// corrupts CallbackContext.control for any other callback still queued for that same event
    /// (e.g. Unity's InputSystemUIInputModule listening to the same "Submit"/"Cancel" action),
    /// throwing IndexOutOfRangeException. Running after the full input update completes avoids
    /// the race.
    /// </summary>
    public static class DeferredInputAction
    {
        public static void Run(Action action)
        {
            void Callback()
            {
                InputSystem.onAfterUpdate -= Callback;
                action();
            }
            InputSystem.onAfterUpdate += Callback;
        }
    }
}
