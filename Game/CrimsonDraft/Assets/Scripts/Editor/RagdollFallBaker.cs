using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.Editor
{
    /// <summary>
    /// Bakes the fall of every <see cref="SocketActivatedRagdoll"/> in a prefab: instantiates the
    /// prefab in an isolated preview scene, drops the ragdolls with the physics of that scene,
    /// records every bone pose along the way and stores the recording in the prefab. At runtime the
    /// recording is played back, so the corpse (and the pickup riding on it) falls and lands exactly
    /// the same every time. Re-bake after changing the room, the corpse's pose or the ragdoll setup.
    /// </summary>
    public static class RagdollFallBaker
    {
        private const string MenuPath = "Assets/CrimsonDraft/Bake Ragdoll Fall";
        private const float Step = 0.02f;
        private const int StepsPerFrame = 2;       // 25 recorded frames per second
        private const int MaxSteps = 500;          // 10 s of simulated time
        private const float StillSeconds = 0.75f;  // body must be still this long before the recording ends

        [MenuItem(MenuPath, true)]
        private static bool ValidateBake()
        {
            return Selection.activeObject is GameObject && PrefabUtility.IsPartOfPrefabAsset(Selection.activeObject);
        }

        [MenuItem(MenuPath)]
        private static void BakeSelected()
        {
            Bake(AssetDatabase.GetAssetPath(Selection.activeObject));
        }

        private sealed class Recording
        {
            public readonly List<Vector3> Positions = new();
            public readonly List<Quaternion> Rotations = new();
            public int Frames;
        }

        public static int Bake(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (!prefab)
            {
                Debug.LogError($"[RagdollFallBaker] No prefab at {prefabPath}");
                return 0;
            }

            Recording[] recordings;

            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var ragdolls = instance.GetComponentsInChildren<SocketActivatedRagdoll>(true);
                recordings = new Recording[ragdolls.Length];

                for (int r = 0; r < ragdolls.Length; r++)
                {
                    recordings[r] = new Recording();
                    ragdolls[r].AppendFrame(recordings[r].Positions, recordings[r].Rotations); // hanging pose
                    recordings[r].Frames++;
                    ragdolls[r].BeginRagdoll();
                }

                var physics = scene.GetPhysicsScene();
                Physics.SyncTransforms();

                var still = new float[ragdolls.Length];
                var done = new bool[ragdolls.Length];
                for (int step = 1; step <= MaxSteps; step++)
                {
                    physics.Simulate(Step);

                    for (int r = 0; r < ragdolls.Length; r++)
                    {
                        if (done[r]) continue;

                        still[r] = ragdolls[r].IsStill() ? still[r] + Step : 0f;
                        bool last = still[r] >= StillSeconds || step == MaxSteps;

                        if (step % StepsPerFrame == 0 || last)
                        {
                            ragdolls[r].AppendFrame(recordings[r].Positions, recordings[r].Rotations);
                            recordings[r].Frames++;
                        }
                        done[r] = last;
                    }

                    if (System.Array.TrueForAll(done, d => d)) break;
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }

            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var targets = contents.GetComponentsInChildren<SocketActivatedRagdoll>(true);
                for (int i = 0; i < targets.Length && i < recordings.Length; i++)
                    targets[i].SetBakedFall(Step * StepsPerFrame, recordings[i].Positions.ToArray(), recordings[i].Rotations.ToArray());

                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            foreach (var recording in recordings)
                Debug.Log($"[RagdollFallBaker] {prefabPath}: baked {recording.Frames} frames ({recording.Frames * Step * StepsPerFrame:0.0} s)");
            return recordings.Length;
        }
    }
}
