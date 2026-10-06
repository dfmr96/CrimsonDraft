#nullable enable

using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace CrimsonDraft.Tests
{
    // Editor-side scene saves can silently drop Wwise's AkInitializer: it is [ExecuteInEditMode]
    // and destroys itself when another scene with one is already open, so saving a combat scene
    // opened additively persists it without Wwise initialization. Guard the serialized file.
    public sealed class CombatSceneIntegrityTests
    {
        private static string ReadScene(string assetPath) =>
            File.ReadAllText(Path.Combine(Path.GetDirectoryName(Application.dataPath)!, assetPath));

        [TestCase("Assets/Scenes/Production/Combat.unity")]
        [TestCase("Assets/Scenes/Test/Combat_Decor.unity")]
        public void CombatScene_keepsWwiseInitializer(string scenePath) =>
            Assert.IsTrue(ReadScene(scenePath).Contains("::AkInitializer"), $"{scenePath} has no AkInitializer component");
    }
}
