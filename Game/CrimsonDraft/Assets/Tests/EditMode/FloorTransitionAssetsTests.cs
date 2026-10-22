#nullable enable

using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.Tests
{
    // The DoorTransition scene plays whatever prefab a floor door hands it and only learns the clip
    // ended through an OnAnimationComplete event; door sounds play only if the clip fires
    // OnDoorOpenSfx. These pin the stairs assets to that contract.
    public sealed class FloorTransitionAssetsTests
    {
        private const string UpPrefab   = "Assets/Prefabs/World/Doors/StairsUpTransition.prefab";
        private const string DownPrefab = "Assets/Prefabs/World/Doors/StairsDownTransition.prefab";

        [TestCase("Assets/Art/Animations/Stairs/StairsUp.anim")]
        [TestCase("Assets/Art/Animations/Stairs/StairsDown.anim")]
        public void StairsClip_endsWithCompletionEvent_andIsSilent(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            Assert.IsNotNull(clip, path);

            var events = AnimationUtility.GetAnimationEvents(clip);
            var complete = events.SingleOrDefault(e => e.functionName == "OnAnimationComplete");
            Assert.IsNotNull(complete, "the transition never ends without it (falls back to the timeout)");
            Assert.AreEqual(clip.length, complete!.time, 0.001f);
            Assert.IsFalse(events.Any(e => e.functionName == "OnDoorOpenSfx"), "stairs must not play the door sounds");
            Assert.IsFalse(AnimationUtility.GetAnimationClipSettings(clip).loopTime);
        }

        [TestCase(UpPrefab)]
        [TestCase(DownPrefab)]
        public void StairsPrefab_isAnimated_andOnTheTransitionCameraLayer(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, path);

            var animator = prefab.GetComponentInChildren<Animator>(true);
            Assert.IsNotNull(animator);
            Assert.IsNotNull(animator!.runtimeAnimatorController);

            int layer = LayerMask.NameToLayer("DoorTransition");
            foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
                Assert.AreEqual(layer, t.gameObject.layer, $"{t.name} would be invisible to the DoorTransition camera");
        }

        // Deck B sits above Deck C: leaving B goes down the stairs, leaving C goes up.
        [TestCase("Assets/Prefabs/World/Rooms/Deck_B/Balcony_A.prefab", "Deck_C_Development", DownPrefab)]
        [TestCase("Assets/Prefabs/World/Rooms/Deck_B/Balcony_B.prefab", "Deck_C_Development", DownPrefab)]
        [TestCase("Assets/Prefabs/World/Rooms/Deck_C/Balcony_A.prefab", "Deck_B_Development", UpPrefab)]
        [TestCase("Assets/Prefabs/World/Rooms/Deck_C/Balcony_B.prefab", "Deck_B_Development", UpPrefab)]
        public void FloorDoor_usesTheStairsTransitionForItsDirection(string roomPath, string targetScene, string expectedPrefab)
        {
            var room = AssetDatabase.LoadAssetAtPath<GameObject>(roomPath);
            Assert.IsNotNull(room, roomPath);

            var door = room.GetComponentsInChildren<SceneDoorInteractable>(true)
                .SingleOrDefault(d => new SerializedObject(d).FindProperty("targetSceneName").stringValue == targetScene);
            Assert.IsNotNull(door, $"no door to {targetScene} in {roomPath}");

            var transition = new SerializedObject(door).FindProperty("doorTransitionPrefab").objectReferenceValue;
            Assert.AreEqual(expectedPrefab, AssetDatabase.GetAssetPath(transition));
        }
    }
}
