#nullable enable

using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CrimsonDraft.Infrastructure.Scenes
{
    public interface ISceneTransitionService
    {
        bool IsInCombat { get; }

        /// <param name="onScreenCovered">
        /// Invoked right after the fade-out finishes covering the screen (before the Combat scene
        /// loads) -- the right moment for a caller to hide/deactivate something in Navigation
        /// (e.g. the enemy that triggered the fight) without it visibly popping away mid-hitstop.
        /// </param>
        UniTask StartCombatAsync(string encounterId, ScriptableObject? encounterAsset = null, bool operatorsStartFull = false, Action? onScreenCovered = null);
    }
}