#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Infrastructure.UI
{
    /// <summary>
    /// Who currently needs the gameplay UI camera. Every UI drawn by it requests the camera when it
    /// opens and releases it when it closes; the camera renders while anyone holds a request
    /// (<see cref="UiCameraAutoToggle"/> applies it). Requests are per owner, so asking twice is
    /// released once, and an owner destroyed without releasing does not pin the camera on.
    /// </summary>
    public static class UiCameraGate
    {
        private static readonly HashSet<object> owners = new();
        private static readonly List<object> dead = new();
        private static bool lastState;

        /// <summary>Raised when the camera goes from not needed to needed, or back.</summary>
        public static event Action? Changed;

        public static bool IsRequested
        {
            get
            {
                Prune();
                return owners.Count > 0;
            }
        }

        public static IReadOnlyCollection<object> Owners
        {
            get
            {
                Prune();
                return owners;
            }
        }

        public static void Request(object owner)
        {
            owners.Add(owner);
            Notify();
        }

        public static void Release(object owner)
        {
            owners.Remove(owner);
            Notify();
        }

        // Statics outlive Play sessions when domain reload is disabled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ResetForTests();

        internal static void ResetForTests()
        {
            owners.Clear();
            lastState = false;
            Changed = null;
        }

        private static void Notify()
        {
            bool state = IsRequested;
            if (state == lastState) return;
            lastState = state;
            Changed?.Invoke();
        }

        // A destroyed UnityEngine.Object compares equal to null but stays in the set.
        private static void Prune()
        {
            dead.Clear();
            foreach (var owner in owners)
                if (owner is UnityEngine.Object unityObject && unityObject == null) dead.Add(owner);
            foreach (var owner in dead) owners.Remove(owner);
        }
    }
}
